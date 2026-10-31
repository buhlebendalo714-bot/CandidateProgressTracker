using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public class MentorCohortQueryService : IMentorCohortQueryService
{
    private readonly TrackerDbContext _db;

    public MentorCohortQueryService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CohortSummary>> GetMyCohortsAsync(
        string mentorUserId)
    {
        if (string.IsNullOrWhiteSpace(mentorUserId))
        {
            return Array.Empty<CohortSummary>();
        }

        var now = DateTimeOffset.UtcNow;

        var mentorRoleHolders =
            from userRole in _db.UserRoles
            join role in _db.Roles
                on userRole.RoleId equals role.Id
            where role.NormalizedName == "MENTOR"
            select userRole.UserId;

        return await _db.Cohorts
            .AsNoTracking()
            .Where(cohort =>
                !cohort.IsArchived &&
                mentorRoleHolders.Contains(mentorUserId) &&
                _db.CohortMemberships.Any(membership =>
                    membership.CohortId == cohort.CohortId &&
                    membership.UserId == mentorUserId &&
                    membership.MembershipKind == MembershipKind.Mentor &&
                    membership.StartsAt <= now &&
                    (membership.EndsAt == null ||
                     membership.EndsAt > now)))
            .OrderBy(cohort => cohort.Name)
            .ThenBy(cohort => cohort.Code)
            .Select(cohort => new CohortSummary(
                cohort.CohortId,
                cohort.Name,
                cohort.Code,
                cohort.LocationLabel,
                cohort.IsArchived))
            .ToListAsync();
    }
}