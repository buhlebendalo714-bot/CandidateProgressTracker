using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public sealed class TeamSubmissionService(TrackerDbContext db) : ITeamSubmissionService
{
    public async Task<TeamSubmissionForm?> GetFormAsync(Guid assignmentId, string userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await AssignmentWorkflow.LockAsync(db, assignmentId);
        var result = await ReadFormAsync(assignmentId, userId);
        await tx.CommitAsync();
        return result;
    }

    private async Task<TeamSubmissionForm?> ReadFormAsync(Guid assignmentId, string userId)
    {
        var target = await TeamSubmissionAccess.TargetAsync(db, assignmentId);
        var now = DateTimeOffset.UtcNow;
        if (target is null || !await TeamSubmissionAccess.CandidateAsync(db, target, userId, now)) return null;
        var member = await TeamSubmissionAccess.MemberAsync(db, target, userId, now);
        if (!member) return null;
        var roster = await TeamSubmissionAccess.RosterAsync(db, assignmentId);
        var eligible = await TeamSubmissionAccess.EligibleAsync(db, target, now);
        var state = await AssignmentWorkflow.LoadAsync(db, assignmentId);
        var revision = state.Revisions.LastOrDefault();
        if (revision is null) return null;
        var approved = state.EffectiveApproval is not null;
        var canSubmit = member && (roster is null || roster.Members.Any(m => m.UserId == userId)) && !approved;
        var preview = roster?.Members ?? eligible;
        var result = new TeamSubmissionForm(new(assignmentId, revision.RevisionId, target.ProjectTitle, target.MilestoneTitle,
            revision.RevisionNo, revision.DueAt, revision.InstructionsSnapshot, revision.AcceptanceSnapshot, revision.EvidencePolicySnapshot),
            target.TeamName, preview, roster, TeamSubmissionAccess.Fingerprint(assignmentId, preview), canSubmit,
            TeamSubmissionAccess.Eligibility(roster, eligible, userId, member, approved));
        return result;
    }

    public async Task<SubmissionResult> SubmitAsync(TeamSubmissionRequest request, string userId)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        if (request is null) return Fail("Invalid submission request.");
        var content = request.Content;
        if (content is null || content.AssignmentId == Guid.Empty || content.AssignmentRevisionId == Guid.Empty
            || content.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return Fail("Invalid submission request.");
        var explanation = content.Explanation?.Trim() ?? "";
        if (explanation.Length is < 1 or > 10000 || content.Evidence is null || content.Evidence.Count is < 1 or > 10)
            return Fail("Provide an explanation of 1–10000 characters and 1–10 evidence links.");
        var evidence = new List<SubmissionEvidenceInput>();
        foreach (var item in content.Evidence)
        {
            if (item is null) return Fail("Check each evidence link and complete its label and secure web address.");
            var label = item.Label?.Trim() ?? "";
            var url = item.Url?.Trim() ?? "";
            if (label.Length is < 1 or > 200 || url.Length is < 1 or > 2000 || !new SubmissionHistoryEvidence(label, url).IsClickable)
                return Fail("Evidence requires labels of 1–200 characters and valid HTTPS URLs of at most 2000 characters.");
            evidence.Add(new(label, url));
        }
        if (evidence.Select(e => e.Url).Distinct(StringComparer.Ordinal).Count() != evidence.Count) return Fail("Remove the duplicate evidence link before submitting.");
        var inserted = new List<object>();
        Assignment? touched = null;
        var committed = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await AssignmentWorkflow.LockAsync(db, content.AssignmentId);
            var target = await TeamSubmissionAccess.TargetAsync(db, content.AssignmentId);
            var now = DateTimeOffset.UtcNow; // One logical instant, after waiting for the locks, for all scheduled boundaries.
            if (target is null || !await TeamSubmissionAccess.CandidateAsync(db, target, userId, now)
                || !await TeamSubmissionAccess.MemberAsync(db, target, userId, now)) return Fail("This team assignment is not available for submission.");
            var roster = await TeamSubmissionAccess.RosterAsync(db, content.AssignmentId);
            if (roster is not null && !roster.Members.Any(m => m.UserId == userId)) return Fail("Only recorded members who still have access to this team and cohort can submit.");
            if (await db.UploadAttempts.AnyAsync(a => a.ActorId == userId && a.RequestId == content.RequestId))
                return Fail("This form has already started a file upload. Open its processing page to check the result.");
            var previous = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(s => s.SubmittedBy == userId && s.RequestId == content.RequestId);
            if (previous is not null)
            {
                var oldEvidence = await db.SubmissionEvidence.Where(e => e.SubmissionId == previous.SubmissionId).OrderBy(e => e.SequenceNo)
                    .Select(e => new SubmissionEvidenceInput(e.Label, e.Url)).ToListAsync();
                if (previous.AssignmentId != content.AssignmentId || previous.AssignmentRevisionId != content.AssignmentRevisionId
                    || previous.Explanation != explanation || !oldEvidence.SequenceEqual(evidence)) return Fail("This form was already used for different content. Reload it before submitting your changes.");
                await tx.CommitAsync();
                return Success(previous);
            }
            var eligible = await TeamSubmissionAccess.EligibleAsync(db, target, now);
            var preview = roster?.Members ?? eligible;
            if (!preview.Any(m => m.UserId == userId) || !string.Equals(request.RosterConfirmation,
                TeamSubmissionAccess.Fingerprint(content.AssignmentId, preview), StringComparison.Ordinal))
                return Fail("The included team members changed or could not be confirmed. Reload the form, review the members and confirm again.");
            var state = await AssignmentWorkflow.LoadAsync(db, content.AssignmentId);
            if (state.EffectiveApproval is not null) return Fail("This assignment is approved. Ask your Administrator if its approval needs to be reopened before submitting again.");
            if (state.Revisions.LastOrDefault()?.RevisionId != content.AssignmentRevisionId) return Fail("The assignment requirements have changed. Reload them before submitting.");
            if (state.Latest?.AttemptNo == int.MaxValue) return Fail("The attempt number limit has been reached.");
            if (roster is null)
            {
                var header = new AssignmentCreditedRoster { AssignmentId = content.AssignmentId, TeamId = target.Assignment.TeamId!.Value,
                    ProjectId = target.Assignment.ProjectId!.Value, CohortId = target.CohortId, TeamNameSnapshot = target.TeamName, FrozenAt = now, FrozenBy = userId };
                db.AssignmentCreditedRosters.Add(header); inserted.Add(header);
                foreach (var member in eligible)
                {
                    var row = new AssignmentCreditedRosterMember { AssignmentId = header.AssignmentId, TeamId = header.TeamId,
                        ProjectId = header.ProjectId, CohortId = header.CohortId, UserId = member.UserId, UserNameSnapshot = member.UserName,
                        TeamMembershipId = member.TeamMembershipId, CohortMembershipId = member.CohortMembershipId };
                    db.AssignmentCreditedRosterMembers.Add(row); inserted.Add(row);
                }
                // Flush the complete roster before attempts reference it, inside this same transaction.
                await db.SaveChangesAsync();
            }
            var submission = new Submission { AssignmentId = content.AssignmentId, CreditedRosterAssignmentId = content.AssignmentId,
                AssignmentRevisionId = content.AssignmentRevisionId, AttemptNo = (state.Latest?.AttemptNo ?? 0) + 1,
                RequestId = content.RequestId, SubmittedBy = userId, Explanation = explanation, ReceivedAt = receivedAt,
                RecordedAt = now < receivedAt ? receivedAt : now };
            db.Submissions.Add(submission); inserted.Add(submission);
            for (var i = 0; i < evidence.Count; i++)
            {
                var row = new SubmissionEvidence { SubmissionId = submission.SubmissionId, SequenceNo = i + 1, Label = evidence[i].Label, Url = evidence[i].Url };
                db.SubmissionEvidence.Add(row); inserted.Add(row);
            }
            touched = await AssignmentWorkflow.TouchAsync(db, content.AssignmentId);
            await db.SaveChangesAsync();
            await tx.CommitAsync(); committed = true;
            return Success(submission);
        }
        catch (Exception error) when (AssignmentWorkflow.Retryable(error)) { return Fail("The team submission changed while you were working. Try submitting this form again, or reload it and confirm the members again."); }
        finally
        {
            if (!committed)
            {
                foreach (var row in inserted) db.Entry(row).State = EntityState.Detached;
                if (touched is not null) db.Entry(touched).State = EntityState.Detached;
            }
        }
    }

    public async Task<SubmissionHistoryPage?> GetHistoryAsync(Guid assignmentId, string userId, int pageNumber = 1)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await AssignmentWorkflow.LockAsync(db, assignmentId);
        var form = await ReadFormAsync(assignmentId, userId);
        if (form is null) return null;
        var result = await HistoryAsync(assignmentId, pageNumber);
        await tx.CommitAsync();
        return result is null ? null : result with { Eligibility = form.Eligibility };
    }
    public async Task<SubmissionHistoryPage?> GetMentorHistoryAsync(Guid assignmentId, string mentorId, int pageNumber = 1)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await AssignmentWorkflow.LockAsync(db, assignmentId);
        if (await TeamSubmissionAccess.TargetAsync(db, assignmentId) is null || !await AssignmentWorkflow.AuthorizedAsync(db, assignmentId, mentorId, "MENTOR")) return null;
        var result = await HistoryAsync(assignmentId, pageNumber);
        await tx.CommitAsync(); return result;
    }
    private async Task<SubmissionHistoryPage?> HistoryAsync(Guid assignmentId, int pageNumber)
    {
        var target = await TeamSubmissionAccess.TargetAsync(db, assignmentId);
        if (target is null) return null;
        pageNumber = Math.Clamp(pageNumber, 1, int.MaxValue / 20);
        var rows = await db.Submissions.AsNoTracking().Where(s => s.AssignmentId == assignmentId)
            .OrderByDescending(s => s.ReceivedAt).ThenByDescending(s => s.AttemptNo).Skip((pageNumber - 1) * 20).Take(21).ToListAsync();
        var state = await AssignmentWorkflow.LoadAsync(db, assignmentId);
        var attempts = new List<SubmissionHistoryAttempt>();
        foreach (var row in rows.Take(20)) attempts.Add(await AssignmentWorkflow.AttemptAsync(db, state, row));
        return new(assignmentId, target.ProjectTitle, target.MilestoneTitle, pageNumber, rows.Count > 20, attempts)
        { CandidateName = target.TeamName, Roster = await TeamSubmissionAccess.RosterAsync(db, assignmentId) };
    }
    public async Task<IReadOnlyList<TeamHistoryLink>> GetCreditedHistoryAsync(string userId)
    {
        var ids = await db.AssignmentCreditedRosterMembers.Where(m => m.UserId == userId).Select(m => m.AssignmentId).ToListAsync();
        var result = new List<TeamHistoryLink>();
        foreach (var id in ids)
        {
            var form = await GetFormAsync(id, userId);
            if (form is not null) result.Add(new(id, form.TeamName, form.Requirements.MilestoneTitle));
        }
        return result;
    }
    private static SubmissionResult Fail(string error) => new(false, null, new[] { error });
    private static SubmissionResult Success(Submission s) => new(true, new(s.SubmissionId, s.AttemptNo, s.ReceivedAt), Array.Empty<string>());
}
