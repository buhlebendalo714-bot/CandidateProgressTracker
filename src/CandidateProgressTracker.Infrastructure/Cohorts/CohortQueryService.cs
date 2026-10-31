using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public class CohortQueryService : ICohortQueryService
{
    private readonly TrackerDbContext _db;

    public CohortQueryService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CohortSummary>> GetCohortsAsync()
    {
        return await _db.Cohorts
            .AsNoTracking()
            .OrderBy(cohort => cohort.Name)
            .ThenBy(cohort => cohort.Code)
            .Take(50)
            .Select(cohort => new CohortSummary(
                cohort.CohortId,
                cohort.Name,
                cohort.Code,
                cohort.LocationLabel,
                cohort.IsArchived))
            .ToListAsync();
    }

    public async Task<CohortDetails?> GetAsync(Guid cohortId)
    {
        return await (
            from cohort in _db.Cohorts.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on cohort.ArchivedBy equals user.Id into archiveUsers
            from archiveUser in archiveUsers.DefaultIfEmpty()
            where cohort.CohortId == cohortId
            select new CohortDetails(
                cohort.CohortId,
                cohort.Name,
                cohort.Code,
                cohort.LocationLabel,
                cohort.IsArchived,
                cohort.RowVersion,
                cohort.ArchivedAt,
                archiveUser == null ? null : archiveUser.UserName,
                cohort.ArchiveReason))
            .SingleOrDefaultAsync();
    }   
}