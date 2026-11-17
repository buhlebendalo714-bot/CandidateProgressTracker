using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class AssignmentHistoryQueryService : IAssignmentHistoryQueryService
{
    private const int PageSize = 20;
    private readonly TrackerDbContext _db;

    public AssignmentHistoryQueryService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<AssignmentHistoryPage?> GetAsync(
        Guid assignmentId,
        string actorUserId,
        int pageNumber = 1)
    {
        if (assignmentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(actorUserId) ||
            pageNumber < 1 ||
            pageNumber > 100000)
        {
            return null;
        }

        var administrators =
            from userRole in _db.UserRoles
            join role in _db.Roles
                on userRole.RoleId equals role.Id
            where role.NormalizedName == "ADMINISTRATOR" && !_db.AccountAccessStates.Any(s => s.UserId == userRole.UserId && !s.Enabled)
            select userRole.UserId;

        if (!await administrators.ContainsAsync(actorUserId)) return null;
        var assignment = await _db.Assignments.AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == assignmentId);
        if (assignment is null) return null;
        var cycle = await _db.LearningCycles.AsNoTracking().SingleAsync(c => c.CycleId == assignment.CycleId);
        var cohort = await _db.Cohorts.AsNoTracking().SingleAsync(c => c.CohortId == cycle.CohortId);
        var title = assignment.OfferingId is not null
            ? await _db.CourseOfferings.Where(o => o.OfferingId == assignment.OfferingId).Select(o => o.TitleSnapshot).SingleAsync()
            : await _db.Milestones.Where(m => m.MilestoneId == assignment.MilestoneId).Select(m => m.Title).SingleAsync();
        var projectArchived = assignment.ProjectId is not null && await _db.Projects.AnyAsync(p => p.ProjectId == assignment.ProjectId && p.IsArchived);
        var owner = assignment.CandidateId is not null
            ? await _db.Users.Where(u => u.Id == assignment.CandidateId).Select(u => u.UserName).SingleAsync()
            : await _db.ProjectTeams.Where(t => t.TeamId == assignment.TeamId).Select(t => t.Name).SingleAsync();
        var header = new { assignment.AssignmentId, MilestoneId = assignment.MilestoneId ?? Guid.Empty, MilestoneTitle = title,
            IsArchived = projectArchived || cycle.IsArchived || cohort.IsArchived, OwnerName = owner ?? "(No username)" };


        var revisions = await (
            from revision in _db.AssignmentRevisions.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on revision.ChangedBy equals user.Id
            where revision.AssignmentId == assignmentId &&
                administrators.Contains(actorUserId)
            orderby revision.RevisionNo descending
            select new AssignmentHistoryEntry(
                revision.RevisionNo,
                revision.DueAt,
                revision.InstructionsSnapshot,
                revision.AcceptanceSnapshot,
                revision.EvidencePolicySnapshot,
                revision.ChangedAt,
                revision.ChangedBy,
                user.UserName ?? "(No username)",
                revision.Reason))
            .Skip((pageNumber - 1) * PageSize)
            .Take(PageSize + 1)
            .ToListAsync();

        return new AssignmentHistoryPage(
            header.AssignmentId,
            header.MilestoneId,
            header.MilestoneTitle,
            header.OwnerName,
            header.IsArchived,
            revisions.Take(PageSize).ToList(),
            pageNumber,
            revisions.Count > PageSize) { CycleId = assignment.CycleId };
    }
}