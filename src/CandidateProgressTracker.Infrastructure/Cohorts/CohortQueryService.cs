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
        return await _db.Cohorts
            .AsNoTracking()
            .Where(cohort => cohort.CohortId == cohortId)
            .Select(cohort => new CohortDetails(
                cohort.CohortId,
                cohort.Name,
                cohort.Code,
                cohort.LocationLabel,
                cohort.IsArchived,
                cohort.RowVersion))
            .SingleOrDefaultAsync();
    }
}