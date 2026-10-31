using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public class CohortMembershipQueryService : ICohortMembershipQueryService
{
    private readonly TrackerDbContext _db;

    public CohortMembershipQueryService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<CohortMembershipDetails?> GetAsync(Guid cohortId)
    {
        var cohort = await _db.Cohorts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CohortId == cohortId);

        if (cohort is null)
        {
            return null;
        }

        var memberships = await (
            from membership in _db.CohortMemberships.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on membership.UserId equals user.Id
            join endingUser in _db.Users.AsNoTracking()
                on membership.EndedBy equals endingUser.Id into endingUsers
            from endingUser in endingUsers.DefaultIfEmpty()
            where membership.CohortId == cohortId
            orderby membership.StartsAt descending,
                membership.MembershipId
            select new MembershipSummary(
                membership.MembershipId,
                user.UserName ?? "(No username)",
                membership.MembershipKind,
                membership.StartsAt,
                membership.EndsAt,
                membership.RowVersion,
                membership.EndReason,
                endingUser == null ? null : endingUser.UserName,
                membership.EndedAt))
            .ToListAsync();

        var eligibleMembers = new List<EligibleMember>();

        if (!cohort.IsArchived)
        {
            var roleHolders = await (
                from user in _db.Users.AsNoTracking()
                join userRole in _db.UserRoles.AsNoTracking()
                    on user.Id equals userRole.UserId
                join role in _db.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where role.Name == "Candidate" || role.Name == "Mentor"
                orderby user.UserName, role.Name
                select new
                {
                    user.Id,
                    user.UserName,
                    RoleName = role.Name
                })
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;

            var blockingMemberships = await _db.CohortMemberships
                .AsNoTracking()
                .Where(item =>
                    item.CohortId == cohortId &&
                    (item.EndsAt == null || item.EndsAt > now))
                .Select(item => new
                {
                    item.UserId,
                    item.MembershipKind
                })
                .ToListAsync();

            var blocked = blockingMemberships
                .Select(item => (item.UserId, item.MembershipKind))
                .ToHashSet();

            foreach (var user in roleHolders)
            {
                var kind = user.RoleName == "Candidate"
                    ? MembershipKind.Candidate
                    : MembershipKind.Mentor;

                if (!blocked.Contains((user.Id, kind)))
                {
                    eligibleMembers.Add(new EligibleMember(
                        user.Id,
                        user.UserName ?? "(No username)",
                        kind));
                }
            }
        }

        return new CohortMembershipDetails(
            cohort.CohortId,
            cohort.Name,
            cohort.Code,
            cohort.IsArchived,
            memberships,
            eligibleMembers);
    }
}