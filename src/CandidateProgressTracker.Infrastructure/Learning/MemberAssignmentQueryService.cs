using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class MemberAssignmentQueryService : IMemberAssignmentQueryService
{
    private const int PageSize = 25;
    private readonly TrackerDbContext _db;

    public MemberAssignmentQueryService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<MemberAssignmentPage?> GetAsync(
        string userId,
        MembershipKind membershipKind,
        int pageNumber = 1)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            pageNumber < 1 ||
            pageNumber > 100000)
        {
            return null;
        }

        var roleName = membershipKind switch
        {
            MembershipKind.Candidate => "CANDIDATE",
            MembershipKind.Mentor => "MENTOR",
            _ => null
        };

        if (roleName is null)
        {
            return null;
        }

        var roleHolders =
            from userRole in _db.UserRoles
            join role in _db.Roles
                on userRole.RoleId equals role.Id
            where role.NormalizedName == roleName
            select userRole.UserId;

        if (!await roleHolders.ContainsAsync(userId))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var isMentor = membershipKind == MembershipKind.Mentor;

        var rows = await (
            from assignment in _db.Assignments.AsNoTracking()
            join project in _db.Projects.AsNoTracking()
                on assignment.ProjectId equals project.ProjectId
            join cycle in _db.LearningCycles.AsNoTracking()
                on project.CycleId equals cycle.CycleId
            join cohort in _db.Cohorts.AsNoTracking()
                on cycle.CohortId equals cohort.CohortId
            join milestone in _db.Milestones.AsNoTracking()
                on assignment.MilestoneId equals milestone.MilestoneId
            join revision in _db.AssignmentRevisions.AsNoTracking()
                on assignment.AssignmentId equals revision.AssignmentId
            where
                !project.IsArchived &&
                !cycle.IsArchived &&
                !cohort.IsArchived &&

                roleHolders.Contains(userId) &&

                _db.CohortMemberships.Any(membership =>
                    membership.CohortId == cohort.CohortId &&
                    membership.UserId == userId &&
                    membership.MembershipKind == membershipKind &&
                    membership.StartsAt <= now &&
                    (membership.EndsAt == null ||
                     membership.EndsAt > now)) &&

                (
                    isMentor ||
                    (
                        project.Mode == ProjectMode.Individual &&
                        assignment.CandidateId == userId &&
                        assignment.TeamId == null
                    ) ||
                    (
                        project.Mode == ProjectMode.Team &&
                        assignment.CandidateId == null &&
                        assignment.TeamId != null &&
                        _db.TeamMemberships.Any(membership =>
                            membership.TeamId == assignment.TeamId &&
                            membership.ProjectId == assignment.ProjectId &&
                            membership.UserId == userId &&
                            membership.StartsAt <= now &&
                            (membership.EndsAt == null ||
                             membership.EndsAt > now))
                    )
                ) &&

                revision.RevisionNo ==
                    _db.AssignmentRevisions
                        .Where(item =>
                            item.AssignmentId == assignment.AssignmentId)
                        .Max(item => item.RevisionNo)

            orderby revision.DueAt, assignment.AssignmentId

            select new MemberAssignmentSummary(
                assignment.AssignmentId,
                cohort.Name,
                cycle.Name,
                project.Title,
                milestone.Title,
                assignment.CandidateId != null
                    ? (_db.Users
                        .Where(user => user.Id == assignment.CandidateId)
                        .Select(user => user.UserName)
                        .FirstOrDefault() ?? "(No username)")
                    : (_db.ProjectTeams
                        .Where(team => team.TeamId == assignment.TeamId)
                        .Select(team => team.Name)
                        .FirstOrDefault() ?? "(Unknown team)"),
                assignment.TeamId != null,
                assignment.IsRequired,
                revision.DueAt,
                revision.RevisionNo,
                revision.InstructionsSnapshot,
                revision.AcceptanceSnapshot,
                revision.EvidencePolicySnapshot))
            .Skip((pageNumber - 1) * PageSize)
            .Take(PageSize + 1)
            .ToListAsync();

        return new MemberAssignmentPage(
            rows.Take(PageSize).ToList(),
            pageNumber,
            rows.Count > PageSize);
    }
}