using System.Data;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Identity;
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

    public async Task<IReadOnlyList<CohortSummary>?> GetCohortsAsync(string actorUserId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await AdministratorAccess.IsCurrentAsync(_db, actorUserId)) return null;
        var result = await _db.Cohorts
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
        await transaction.CommitAsync();
        return result;
    }

    public async Task<CohortDetails?> GetAsync(Guid cohortId, string actorUserId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await AdministratorAccess.IsCurrentAsync(_db, actorUserId)) return null;
        var result = await (
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
        await transaction.CommitAsync();
        return result;
    }   
}
