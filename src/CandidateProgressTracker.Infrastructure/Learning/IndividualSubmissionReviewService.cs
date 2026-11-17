using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public sealed class IndividualSubmissionReviewService(TrackerDbContext db, IPrivateEvidenceStorage? storage = null)
    : IIndividualSubmissionReviewService, IAssignmentReopeningService
{
    public Task<ReviewPage?> GetReviewAsync(Guid assignmentId, string mentorId) => GetAsync(assignmentId, mentorId, "MENTOR");
    public Task<ReviewPage?> GetReopeningAsync(Guid assignmentId, string administratorId) => GetAsync(assignmentId, administratorId, "ADMINISTRATOR");

    private async Task<ReviewPage?> GetAsync(Guid assignmentId, string actorId, string role)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await AssignmentWorkflow.LockAsync(db, assignmentId);
        if (!await AssignmentWorkflow.AuthorizedAsync(db, assignmentId, actorId, role)) return null;
        var assignment = await db.Assignments.AsNoTracking().SingleAsync(a => a.AssignmentId == assignmentId);
        var state = await AssignmentWorkflow.LoadAsync(db, assignmentId);
        var latest = state.Latest;
        var self = latest is not null && await AssignmentWorkflow.SelfReviewAsync(db, latest, actorId);
        var filesAvailable = latest is null || await FilesAvailableAsync(latest.SubmissionId);
        var canDecide = role == "MENTOR" && state.CanReview && !self && filesAvailable;
        var eligibility = !filesAvailable ? "Evidence is missing or unavailable. Review is blocked until integrity is restored." : role == "MENTOR" && self ? "You cannot review your own submission. Credited roster members also cannot review shared work." : state.Eligibility;
        var name = assignment.TeamId is not null
            ? await db.ProjectTeams.Where(t => t.TeamId == assignment.TeamId).Select(t => t.Name).SingleAsync()
            : await db.Users.Where(u => u.Id == assignment.CandidateId).Select(u => u.UserName).SingleAsync();
        var roster = await TeamSubmissionAccess.RosterAsync(db, assignmentId);
        var attempt = latest is null ? null : await AssignmentWorkflow.AttemptAsync(db, state, latest);
        await transaction.CommitAsync();
        return new(assignmentId, name ?? "(No username)", Convert.ToBase64String(assignment.RowVersion),
            attempt, canDecide, eligibility, state.EffectiveApproval?.ReviewId) { Roster = roster };
    }

    public async Task<WorkflowResult> DecideAsync(ReviewRequest request, string mentorId)
    {
        var feedback = request.Feedback?.Trim() ?? "";
        if (request.RequestId == Guid.Empty || request.SubmissionId == Guid.Empty
            || !Enum.IsDefined(request.Decision) || feedback.Length > ReviewLimits.Feedback
            || (request.Decision == ReviewDecision.ChangesRequested && feedback.Length == 0))
            return Fail("Choose a valid decision. Changes requested requires feedback; feedback may contain at most 10000 characters.");
        return await WriteAsync(request.AssignmentId, mentorId, "MENTOR", async (assignment, state) =>
        {
            var submission = state.Submissions.SingleOrDefault(s => s.SubmissionId == request.SubmissionId);
            if (submission is null || await AssignmentWorkflow.SelfReviewAsync(db, submission, mentorId)) return Fail("This submission is not available for review.");
            var previous = await db.SubmissionReviews.AsNoTracking().SingleOrDefaultAsync(r => r.ReviewerId == mentorId && r.RequestId == request.RequestId);
            if (previous is not null)
                return previous.AssignmentId == request.AssignmentId && previous.SubmissionId == request.SubmissionId
                    && previous.Decision == request.Decision && previous.Feedback == feedback
                    ? Success(previous.ReviewId) : Fail("This form was already used for different content. Reload it before submitting your changes.");
            if (!Matches(request.RowVersion, assignment.RowVersion)) return Stale();
            if (state.Latest?.SubmissionId != request.SubmissionId) return Fail("Only the latest attempt may receive a new decision.");
            if (!state.CanReview) return Fail(state.Eligibility);
            if (!await FilesAvailableAsync(submission.SubmissionId)) return Fail("Evidence is unavailable or still being checked. Wait for file checks to finish before reviewing; contact your Administrator if it stays unavailable.");
            if (state.Reviews.LastOrDefault()?.ReviewNo == int.MaxValue) return Fail("The review number limit has been reached.");
            var review = new SubmissionReview
            {
                AssignmentId = request.AssignmentId, SubmissionId = request.SubmissionId,
                ReviewNo = (state.Reviews.LastOrDefault()?.ReviewNo ?? 0) + 1,
                Decision = request.Decision, Feedback = feedback, ReviewerId = mentorId,
                DecidedAt = DateTimeOffset.UtcNow, RequestId = request.RequestId
            };
            db.SubmissionReviews.Add(review);
            await AssignmentWorkflow.TouchAsync(db, request.AssignmentId);
            await db.SaveChangesAsync();
            return Success(review.ReviewId);
        });
    }

    public async Task<WorkflowResult> ReopenAsync(ReopeningRequest request, string administratorId)
    {
        var reason = request.Reason?.Trim() ?? "";
        if (request.RequestId == Guid.Empty || request.ApprovalReviewId == Guid.Empty || reason.Length is < 1 or > ReviewLimits.ReopeningReason)
            return Fail("Enter a reopening reason of 1–1000 characters and a valid request ID.");
        return await WriteAsync(request.AssignmentId, administratorId, "ADMINISTRATOR", async (assignment, state) =>
        {
            var previous = await db.AssignmentReopenings.AsNoTracking().SingleOrDefaultAsync(r => r.AdministratorId == administratorId && r.RequestId == request.RequestId);
            if (previous is not null)
                return previous.AssignmentId == request.AssignmentId && previous.ApprovalReviewId == request.ApprovalReviewId && previous.Reason == reason
                    ? Success(previous.ReopeningId) : Fail("This form was already used for different content. Reload it before submitting your changes.");
            if (!Matches(request.RowVersion, assignment.RowVersion)) return Stale();
            if (state.EffectiveApproval?.ReviewId != request.ApprovalReviewId) return Fail("Only an approval that is still in force can be reopened.");
            var reopening = new AssignmentReopening
            {
                AssignmentId = request.AssignmentId, ApprovalReviewId = request.ApprovalReviewId,
                AdministratorId = administratorId, Reason = reason, ReopenedAt = DateTimeOffset.UtcNow, RequestId = request.RequestId
            };
            db.AssignmentReopenings.Add(reopening);
            await AssignmentWorkflow.TouchAsync(db, request.AssignmentId);
            await db.SaveChangesAsync();
            return Success(reopening.ReopeningId);
        });
    }

    private async Task<WorkflowResult> WriteAsync(Guid assignmentId, string actorId, string role,
        Func<Assignment, WorkflowState, Task<WorkflowResult>> action)
    {
        // Remember only entities this operation adds/touches; never detach unrelated work.
        var before = db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        var committed = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await AssignmentWorkflow.LockAsync(db, assignmentId);
            if (!await AssignmentWorkflow.AuthorizedAsync(db, assignmentId, actorId, role)) return Fail("This assignment is not available.");
            var assignment = await db.Assignments.AsNoTracking().SingleAsync(a => a.AssignmentId == assignmentId);
            var state = await AssignmentWorkflow.LoadAsync(db, assignmentId);
            var result = await action(assignment, state);
            if (result.Succeeded) { await transaction.CommitAsync(); committed = true; }
            return result;
        }
        catch (Exception exception) when (AssignmentWorkflow.Retryable(exception)) { return Stale(); }
        finally
        {
            if (!committed)
            {
                foreach (var entry in db.ChangeTracker.Entries().ToList())
                    if ((!before.Contains(entry.Entity) && entry.Entity is SubmissionReview or AssignmentReopening)
                        || (entry.Entity is Assignment a && a.AssignmentId == assignmentId && entry.State == EntityState.Modified))
                        entry.State = EntityState.Detached;
            }
        }
    }

    private async Task<bool> FilesAvailableAsync(Guid id) => storage is not null
        ? await FileEvidenceService.ReviewableAsync(db, storage, id)
        : !await db.UploadAttempts.AnyAsync(a => a.SubmissionId == id);

    private static bool Matches(string token, byte[] version)
    {
        var decoded = new byte[8];
        return Convert.TryFromBase64String(token ?? "", decoded, out var written)
            && written == 8 && decoded.SequenceEqual(version);
    }
    private static WorkflowResult Success(Guid receipt) => new(true, receipt, false, Array.Empty<string>());
    private static WorkflowResult Fail(string error) => new(false, null, false, new[] { error });
    private static WorkflowResult Stale() => new(false, null, true, new[] { "This assignment changed. Reload before making a new decision, or retry the identical accepted request." });
}
