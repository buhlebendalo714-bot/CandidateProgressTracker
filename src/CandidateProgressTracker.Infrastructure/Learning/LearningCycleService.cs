using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class LearningCycleService : ILearningCycleService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public LearningCycleService(
        TrackerDbContext db,
        UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<bool> IsAdministratorAsync(string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return false;
        }

        var user = await _users.FindByIdAsync(actorUserId);

        return user is not null &&
            await _users.IsInRoleAsync(user, "Administrator");
    }

    public async Task<CohortCycles?> GetForCohortAsync(
        Guid cohortId,
        string actorUserId)
    {
        if (!await IsAdministratorAsync(actorUserId))
        {
            return null;
        }

        var cohort = await _db.Cohorts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CohortId == cohortId);

        if (cohort is null)
        {
            return null;
        }

        var cycles = await _db.LearningCycles
            .AsNoTracking()
            .Where(cycle => cycle.CohortId == cohortId)
            .OrderByDescending(cycle => cycle.StartsOn)
            .ThenBy(cycle => cycle.Name)
            .ThenBy(cycle => cycle.CycleId)
            .Select(cycle => new LearningCycleSummary(
                cycle.CycleId,
                cycle.Name,
                cycle.StartsOn,
                cycle.EndsOn,
                cycle.IsArchived))
            .ToListAsync();

        return new CohortCycles(
            cohort.CohortId,
            cohort.Name,
            cohort.IsArchived,
            cycles);
    }

    public async Task<LearningCycleResult> CreateAsync(
        Guid cohortId,
        string name,
        DateOnly startsOn,
        DateOnly endsOn,
        string actorUserId)
    {
        static LearningCycleResult Failure(string message) =>
            new(false, null, new[] { message });

        var trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length is < 1 or > 150)
        {
            return Failure("Enter a cycle name of up to 150 characters.");
        }

        if (startsOn == default || endsOn == default)
        {
            return Failure("Enter both the start and end dates.");
        }

        if (endsOn < startsOn)
        {
            return Failure("The end date cannot be before the start date.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var cohort = await _db.Cohorts
            .SingleOrDefaultAsync(item => item.CohortId == cohortId);

        if (cohort is null)
        {
            return Failure("The selected cohort was not found.");
        }

        if (cohort.IsArchived)
        {
            return Failure("You cannot create a cycle in an archived cohort.");
        }

        var duplicate = await _db.LearningCycles.AnyAsync(cycle =>
            cycle.CohortId == cohortId &&
            cycle.Name == trimmedName);

        if (duplicate)
        {
            return Failure("A cycle with this name already exists in this cohort.");
        }

        var cycle = new LearningCycle
        {
            CohortId = cohortId,
            Name = trimmedName,
            StartsOn = startsOn,
            EndsOn = endsOn,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorUserId
        };

        _db.LearningCycles.Add(cycle);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new LearningCycleResult(
                true, cycle.CycleId, Array.Empty<string>());
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627) &&
                  sql.Message.Contains("IX_LearningCycles_CohortId_Name"))
        {
            await transaction.RollbackAsync();
            _db.Entry(cycle).State = EntityState.Detached;

            return Failure(
                "A cycle with this name already exists in this cohort.");
        }
    }
}