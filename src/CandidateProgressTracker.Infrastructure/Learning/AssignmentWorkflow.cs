using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

internal static class AssignmentWorkflow
{
    // Every workflow transaction takes this lock before reading authorization,
    // revisions, submissions or decisions. Retained until commit/rollback.
    internal static async Task LockAsync(TrackerDbContext db, Guid assignmentId)
    {
        await CohortWorkflowGate.ForAssignmentAsync(db, assignmentId);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [AssignmentId] FROM [Assignments] WITH (UPDLOCK, HOLDLOCK) WHERE [AssignmentId] = {assignmentId}");
    }

    internal static async Task<Assignment> TouchAsync(TrackerDbContext db, Guid id)
    {
        var assignment = await db.Assignments.FindAsync(id)
            ?? throw new InvalidOperationException("Assignment disappeared during workflow transaction.");
        await db.Entry(assignment).ReloadAsync();
        // Mirrors the existing requirements-only revision update, advancing rowversion.
        db.Entry(assignment).Property(a => a.DueAt).IsModified = true;
        return assignment;
    }

    internal static async Task<bool> AuthorizedAsync(TrackerDbContext db, Guid assignmentId, string actorId, string role)
    {
        var target = await AssignmentAccess.TargetAsync(db, assignmentId);
        return target is not null && await AssignmentAccess.StaffAsync(db, target, actorId, role);
    }

    internal static bool Retryable(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException sql => sql.Number is -2 or 1205 or 2601 or 2627 or 51010,
        DbUpdateException or InvalidOperationException => exception.InnerException is not null && Retryable(exception.InnerException),
        _ => false
    };

    internal static async Task<bool> SelfReviewAsync(TrackerDbContext db, Submission submission, string actor) =>
        submission.SubmittedBy == actor || await db.AssignmentCreditedRosterMembers.AnyAsync(m => m.AssignmentId == submission.AssignmentId && m.UserId == actor);

    internal static async Task<WorkflowState> LoadAsync(TrackerDbContext db, Guid assignmentId) => new(
        await db.AssignmentRevisions.AsNoTracking().Where(r => r.AssignmentId == assignmentId).OrderBy(r => r.RevisionNo).ToListAsync(),
        await db.Submissions.AsNoTracking().Where(s => s.AssignmentId == assignmentId).OrderBy(s => s.AttemptNo).ToListAsync(),
        await db.SubmissionReviews.AsNoTracking().Where(r => r.AssignmentId == assignmentId).OrderBy(r => r.ReviewNo).ToListAsync(),
        await db.AssignmentReopenings.AsNoTracking().Where(r => r.AssignmentId == assignmentId).ToListAsync());

    internal static async Task<IReadOnlyList<ReviewHistory>> HistoryAsync(TrackerDbContext db, WorkflowState state, Guid submissionId)
    {
        var reviews = state.Reviews.Where(r => r.SubmissionId == submissionId).OrderByDescending(r => r.ReviewNo).ToList();
        var ids = reviews.Select(r => r.ReviewerId).Concat(state.Reopenings.Select(r => r.AdministratorId)).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName ?? "(No username)");
        return reviews.Select(r =>
        {
            var reopening = state.Reopenings.SingleOrDefault(o => o.ApprovalReviewId == r.ReviewId);
            return new ReviewHistory(r.ReviewId, r.ReviewNo, r.Decision, r.Feedback,
                names[r.ReviewerId], r.DecidedAt, state.Status(r), reopening is null ? null :
                new ReopeningHistory(reopening.ReopeningId, names[reopening.AdministratorId], reopening.ReopenedAt, reopening.Reason));
        }).ToList();
    }

    internal static async Task<SubmissionHistoryAttempt> AttemptAsync(TrackerDbContext db, WorkflowState state, Submission submission)
    {
        var revision = state.Revisions.Single(r => r.RevisionId == submission.AssignmentRevisionId);
        var evidence = await db.SubmissionEvidence.AsNoTracking().Where(e => e.SubmissionId == submission.SubmissionId)
            .OrderBy(e => e.SequenceNo).Select(e => new SubmissionHistoryEvidence(e.Label, e.Url)).ToListAsync();
        return new(submission.SubmissionId, submission.AttemptNo, submission.ReceivedAt, submission.Explanation,
            revision.RevisionId, revision.RevisionNo, revision.DueAt, revision.InstructionsSnapshot,
            revision.AcceptanceSnapshot, revision.EvidencePolicySnapshot, evidence)
        { Reviews = await HistoryAsync(db, state, submission.SubmissionId),
            Files = await (from f in db.EvidenceFiles join a in db.UploadAttempts on f.UploadAttemptId equals a.UploadAttemptId where a.SubmissionId == submission.SubmissionId && a.Status == "Committed" select new SubmissionFile(f.EvidenceFileId, f.OriginalName, f.SizeBytes)).ToListAsync(),
            SubmitterName = await db.Users.Where(u => u.Id == submission.SubmittedBy).Select(u => u.UserName).SingleAsync() };
    }
}

internal sealed record WorkflowState(List<AssignmentRevision> Revisions, List<Submission> Submissions,
    List<SubmissionReview> Reviews, List<AssignmentReopening> Reopenings)
{
    internal Submission? Latest => Submissions.LastOrDefault();
    internal SubmissionReview? LatestReview => Latest is null ? null : Reviews.LastOrDefault(r => r.SubmissionId == Latest.SubmissionId);

    internal bool Eligible(Submission submission)
    {
        var original = Revisions.Single(r => r.RevisionId == submission.AssignmentRevisionId);
        // Compare every intervening snapshot in memory using ordinal comparison.
        // A change followed by a revert must never revive an old attempt/approval.
        return !Revisions.Where(r => r.RevisionNo > original.RevisionNo).Any(r =>
            !string.Equals(r.InstructionsSnapshot, original.InstructionsSnapshot, StringComparison.Ordinal)
            || !string.Equals(r.AcceptanceSnapshot, original.AcceptanceSnapshot, StringComparison.Ordinal)
            || !string.Equals(r.EvidencePolicySnapshot, original.EvidencePolicySnapshot, StringComparison.Ordinal));
    }

    internal SubmissionReview? EffectiveApproval => Latest is not null && Eligible(Latest)
        && LatestReview?.Decision == ReviewDecision.Approved
        && !Reopenings.Any(r => r.ApprovalReviewId == LatestReview.ReviewId) ? LatestReview : null;

    internal bool CanReview => Latest is not null && Eligible(Latest) &&
        (LatestReview is null || (LatestReview.Decision == ReviewDecision.Approved
            && Reopenings.Any(r => r.ApprovalReviewId == LatestReview.ReviewId)));

    internal string Eligibility => Latest is null ? "No submission is available to review yet."
        : !Eligible(Latest) ? "Requirements changed. A fresh submission is required, even if requirements were later reverted."
        : LatestReview?.Decision == ReviewDecision.ChangesRequested ? "Changes requested. A new attempt is required before another decision."
        : EffectiveApproval is not null ? "This submission is approved. A new submission needs an Administrator to reopen approval or change the requirements."
        : "The latest attempt is eligible for review.";

    internal string Status(SubmissionReview review)
    {
        if (review.Decision == ReviewDecision.ChangesRequested) return "Changes requested";
        if (!Eligible(Submissions.Single(s => s.SubmissionId == review.SubmissionId))) return "Historical approval — requirements changed";
        if (Reopenings.Any(r => r.ApprovalReviewId == review.ReviewId)) return "Reopened approval";
        return EffectiveApproval?.ReviewId == review.ReviewId ? "Effective approval" : "Historical approval — superseded";
    }
}
