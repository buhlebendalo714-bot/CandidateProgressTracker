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
            where role.NormalizedName == "MENTOR" && !_db.AccountAccessStates.Any(s => s.UserId == userRole.UserId && !s.Enabled)
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

    public async Task<MentorCohortDetails?> GetDetailsAsync(
        Guid cohortId,
        string mentorUserId)
    {
        if (cohortId == Guid.Empty ||
            string.IsNullOrWhiteSpace(mentorUserId))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        var mentorRoleHolders =
            from userRole in _db.UserRoles
            join role in _db.Roles
                on userRole.RoleId equals role.Id
            where role.NormalizedName == "MENTOR" && !_db.AccountAccessStates.Any(s => s.UserId == userRole.UserId && !s.Enabled)
            select userRole.UserId;

        // Keep authorization and candidate retrieval in one SQL query.
        // The left join preserves an authorized cohort with no candidates.
        var currentCandidates =
            from membership in _db.CohortMemberships.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.MembershipKind == MembershipKind.Candidate
                && membership.StartsAt <= now
                && (membership.EndsAt == null || membership.EndsAt > now)
            select new
            {
                membership.CohortId,
                UserId = user.Id,
                UserName = user.UserName ?? "(No username)"
            };

        var rows = await (
            from cohort in _db.Cohorts.AsNoTracking()
            where cohort.CohortId == cohortId
                && !cohort.IsArchived
                && mentorRoleHolders.Contains(mentorUserId)
                && _db.CohortMemberships.Any(membership =>
                    membership.CohortId == cohort.CohortId &&
                    membership.UserId == mentorUserId &&
                    membership.MembershipKind == MembershipKind.Mentor &&
                    membership.StartsAt <= now &&
                    (membership.EndsAt == null || membership.EndsAt > now))
            join candidate in currentCandidates
                on cohort.CohortId equals candidate.CohortId
                into candidates
            from candidate in candidates.DefaultIfEmpty()
            select new
            {
                cohort.CohortId,
                cohort.Name,
                cohort.Code,
                cohort.LocationLabel,
                CandidateUserId = candidate == null ? null : candidate.UserId,
                CandidateUserName = candidate == null ? null : candidate.UserName
            })
            .ToListAsync();

        if (rows.Count == 0)
        {
            return null;
        }

        var first = rows[0];

        var candidateSummaries = rows
            .Where(row => row.CandidateUserId != null)
            .Select(row => new MentorCandidateSummary(
                row.CandidateUserId!,
                row.CandidateUserName!))
            .DistinctBy(candidate => candidate.UserId)
            .OrderBy(candidate => candidate.UserName)
            .ThenBy(candidate => candidate.UserId)
            .ToList();

        return new MentorCohortDetails(
            first.CohortId,
            first.Name,
            first.Code,
            first.LocationLabel,
            candidateSummaries);
    }
}