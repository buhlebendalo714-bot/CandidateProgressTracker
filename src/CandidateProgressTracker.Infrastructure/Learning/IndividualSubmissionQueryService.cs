using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public sealed class IndividualSubmissionQueryService
    : IIndividualSubmissionQueryService
{
    private readonly TrackerDbContext _db;

    public IndividualSubmissionQueryService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<SubmissionHistoryPage?> GetHistoryAsync(
        Guid assignmentId, string userId, int pageNumber = 1)
    {
        // Reuse the submission eligibility checks, including the current database role.
        var accessible = await GetAsync(assignmentId, userId);
        if (accessible is null) return null;

        return await LoadHistoryAsync(assignmentId, userId, accessible.ProjectTitle,
            accessible.MilestoneTitle, pageNumber);
    }

    public async Task<SubmissionHistoryPage?> GetMentorHistoryAsync(
        Guid assignmentId, string mentorUserId, int pageNumber = 1)
    {
        var t = await AssignmentAccess.TargetAsync(_db, assignmentId);
        if (t is null || t.Assignment.TeamId is not null || !await AssignmentAccess.StaffAsync(_db, t, mentorUserId, "MENTOR")) return null;
        var h = await LoadHistoryAsync(assignmentId, t.Assignment.CandidateId!, t.Group, t.Title, pageNumber);
        return h with { CandidateName = await _db.Users.Where(u => u.Id == t.Assignment.CandidateId).Select(u => u.UserName).SingleAsync() };
    }

    private async Task<SubmissionHistoryPage> LoadHistoryAsync(Guid assignmentId, string userId,
        string projectTitle, string milestoneTitle, int pageNumber)
    {
        pageNumber = Math.Clamp(pageNumber, 1, int.MaxValue / 20);
        var rows = await (
            from submission in _db.Submissions.AsNoTracking()
            join revision in _db.AssignmentRevisions.AsNoTracking()
                on new { Id = submission.AssignmentRevisionId, submission.AssignmentId }
                equals new { Id = revision.RevisionId, revision.AssignmentId }
            where submission.AssignmentId == assignmentId && submission.SubmittedBy == userId
            orderby submission.ReceivedAt descending, submission.AttemptNo descending
            select new { submission, revision })
            .Skip((pageNumber - 1) * 20).Take(21).ToListAsync();

        var ids = rows.Take(20).Select(row => row.submission.SubmissionId).ToArray();
        var evidence = await _db.SubmissionEvidence.AsNoTracking()
            .Where(item => ids.Contains(item.SubmissionId))
            .OrderBy(item => item.SequenceNo).ToListAsync();
        var attempts = rows.Take(20).Select(row => new SubmissionHistoryAttempt(
            row.submission.SubmissionId, row.submission.AttemptNo, row.submission.ReceivedAt,
            row.submission.Explanation, row.revision.RevisionId, row.revision.RevisionNo,
            row.revision.DueAt, row.revision.InstructionsSnapshot,
            row.revision.AcceptanceSnapshot, row.revision.EvidencePolicySnapshot,
            evidence.Where(item => item.SubmissionId == row.submission.SubmissionId)
                .Select(item => new SubmissionHistoryEvidence(item.Label, item.Url)).ToList())).ToList();
        var workflow = await AssignmentWorkflow.LoadAsync(_db, assignmentId);
        for (var i = 0; i < attempts.Count; i++)
            attempts[i] = attempts[i] with { Reviews = await AssignmentWorkflow.HistoryAsync(_db, workflow, attempts[i].ReceiptId), Files = (await AssignmentWorkflow.AttemptAsync(_db, workflow, rows[i].submission)).Files };
        return new(assignmentId, projectTitle, milestoneTitle,
            pageNumber, rows.Count > 20, attempts);
    }

    public async Task<IndividualSubmissionDetails?> GetAsync(
        Guid assignmentId,
        string userId)
    {
        var t = await AssignmentAccess.TargetAsync(_db, assignmentId);
        if (t is null || t.Assignment.TeamId is not null || !await AssignmentAccess.CandidateAsync(_db, t, userId, false)) return null;
        var revision = await _db.AssignmentRevisions.AsNoTracking().Where(r => r.AssignmentId == assignmentId).OrderByDescending(r => r.RevisionNo).FirstOrDefaultAsync();
        return revision is null ? null : new(assignmentId, revision.RevisionId, t.Group, t.Title, revision.RevisionNo, revision.DueAt,
            revision.InstructionsSnapshot, revision.AcceptanceSnapshot, revision.EvidencePolicySnapshot);
    }
}
