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
            where role.NormalizedName == "ADMINISTRATOR"
            select userRole.UserId;

        var header = await (
            from assignment in _db.Assignments.AsNoTracking()
            join milestone in _db.Milestones.AsNoTracking()
                on assignment.MilestoneId equals milestone.MilestoneId
            join project in _db.Projects.AsNoTracking()
                on assignment.ProjectId equals project.ProjectId
            join cycle in _db.LearningCycles.AsNoTracking()
                on project.CycleId equals cycle.CycleId
            join cohort in _db.Cohorts.AsNoTracking()
                on cycle.CohortId equals cohort.CohortId
            where assignment.AssignmentId == assignmentId &&
                administrators.Contains(actorUserId)
            select new
            {
                assignment.AssignmentId,
                milestone.MilestoneId,
                MilestoneTitle = milestone.Title,
                IsArchived =
                    project.IsArchived ||
                    cycle.IsArchived ||
                    cohort.IsArchived,
                OwnerName = assignment.CandidateId != null
                    ? (_db.Users
                        .Where(user => user.Id == assignment.CandidateId)
                        .Select(user => user.UserName)
                        .FirstOrDefault() ?? "(No username)")
                    : (_db.ProjectTeams
                        .Where(team => team.TeamId == assignment.TeamId)
                        .Select(team => team.Name)
                        .FirstOrDefault() ?? "(Unknown team)")
            })
            .SingleOrDefaultAsync();

        if (header is null)
        {
            return null;
        }

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
            revisions.Count > PageSize);
    }
}