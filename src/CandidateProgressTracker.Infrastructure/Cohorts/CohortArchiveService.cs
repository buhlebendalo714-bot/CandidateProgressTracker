using System.Data;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public class CohortArchiveService : ICohortArchiveService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public CohortArchiveService(
        TrackerDbContext db,
        UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<CohortArchiveResult> ArchiveAsync(
        Guid cohortId,
        string reason,
        byte[] expectedRowVersion,
        string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        if (cohortId == Guid.Empty)
        {
            return Failure("Select a cohort.");
        }

        var cleanReason = reason?.Trim() ?? string.Empty;

        if (cleanReason.Length == 0 || cleanReason.Length > 1000)
        {
            return Failure(
                "Enter an archive reason of up to 1000 characters.");
        }

        if (expectedRowVersion is null || expectedRowVersion.Length != 8)
        {
            return Failure(
                "The cohort version is missing or invalid. Reload the page.");
        }

        await using var transaction = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var actor = await _userManager.FindByIdAsync(actorUserId);

        if (actor is null ||
            !await _userManager.IsInRoleAsync(actor, "Administrator"))
        {
            return Failure("Administrator access is required.");
        }

        var cohort = await _db.Cohorts
            .SingleOrDefaultAsync(item => item.CohortId == cohortId);

        if (cohort is null)
        {
            return Failure("The selected cohort was not found.");
        }

        if (!cohort.RowVersion.SequenceEqual(expectedRowVersion))
        {
            return Failure(
                "This cohort has changed. Reload the page before trying again.");
        }

        if (cohort.IsArchived)
        {
            return Failure("This cohort is already archived.");
        }

        cohort.IsArchived = true;
        cohort.ArchivedAt = DateTimeOffset.UtcNow;
        cohort.ArchivedBy = actor.Id;
        cohort.ArchiveReason = cleanReason;

        _db.Entry(cohort)
            .Property(item => item.RowVersion)
            .OriginalValue = expectedRowVersion;

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            _db.Entry(cohort).State = EntityState.Detached;

            return Failure(
                "This cohort has changed. Reload the page before trying again.");
        }

        return new CohortArchiveResult(
            true,
            Array.Empty<string>());
    }

    private static CohortArchiveResult Failure(string message)
    {
        return new CohortArchiveResult(
            false,
            new[] { message });
    }
}