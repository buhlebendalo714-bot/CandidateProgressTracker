using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class MemberCycleQueryService : IMemberCycleQueryService
{
    private readonly TrackerDbContext _db;

    public MemberCycleQueryService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<CohortCycles?> GetAsync(
        Guid cohortId,
        string userId,
        MembershipKind membershipKind)
    {
        if (cohortId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var roleName = membershipKind switch
        {
            MembershipKind.Mentor => "MENTOR",
            MembershipKind.Candidate => "CANDIDATE",
            _ => null
        };

        if (roleName is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        var roleHolders =
            from userRole in _db.UserRoles
            join role in _db.Roles
                on userRole.RoleId equals role.Id
            where role.NormalizedName == roleName
            select userRole.UserId;

        var visibleCycles = _db.LearningCycles
            .AsNoTracking()
            .Where(cycle => !cycle.IsArchived);

        var rows = await (
            from cohort in _db.Cohorts.AsNoTracking()
            where cohort.CohortId == cohortId
                && !cohort.IsArchived
                && roleHolders.Contains(userId)
                && _db.CohortMemberships.Any(membership =>
                    membership.CohortId == cohort.CohortId &&
                    membership.UserId == userId &&
                    membership.MembershipKind == membershipKind &&
                    membership.StartsAt <= now &&
                    (membership.EndsAt == null ||
                     membership.EndsAt > now))
            join cycle in visibleCycles
                on cohort.CohortId equals cycle.CohortId into cycles
            from cycle in cycles.DefaultIfEmpty()
            select new
            {
                cohort.CohortId,
                CohortName = cohort.Name,
                CycleId = cycle == null ? (Guid?)null : cycle.CycleId,
                CycleName = cycle == null ? null : cycle.Name,
                StartsOn = cycle == null ? (DateOnly?)null : cycle.StartsOn,
                EndsOn = cycle == null ? (DateOnly?)null : cycle.EndsOn
            })
            .ToListAsync();

        if (rows.Count == 0)
        {
            return null;
        }

        var summaries = rows
            .Where(row => row.CycleId.HasValue)
            .Select(row => new LearningCycleSummary(
                row.CycleId!.Value,
                row.CycleName!,
                row.StartsOn!.Value,
                row.EndsOn!.Value,
                false))
            .OrderByDescending(cycle => cycle.StartsOn)
            .ThenBy(cycle => cycle.Name)
            .ThenBy(cycle => cycle.CycleId)
            .ToList();

        return new CohortCycles(
            rows[0].CohortId,
            rows[0].CohortName,
            false,
            summaries);
    }
}