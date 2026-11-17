using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public class CohortCreationService : ICohortCreationService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public CohortCreationService(
        TrackerDbContext db,
        UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<CohortCreationResult> CreateAsync(
        string name,
        string code,
        string? locationLabel,
        string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var actor = await _userManager.FindByIdAsync(actorUserId);

        if (actor is null ||
            !(await CandidateProgressTracker.Infrastructure.Identity.AccountAvailability.IsActiveAsync(_db, actor.Id) && await _userManager.IsInRoleAsync(actor, "Administrator")))
        {
            return Failure("Administrator access is required.");
        }

        var cleanName = name?.Trim() ?? string.Empty;
        var cleanCode = code?.Trim().ToUpperInvariant() ?? string.Empty;
        var cleanLocation = string.IsNullOrWhiteSpace(locationLabel)
            ? null
            : locationLabel.Trim();

        var errors = new List<string>();

        if (cleanName.Length == 0 || cleanName.Length > 150)
        {
            errors.Add("Enter a cohort name of up to 150 characters.");
        }

        if (cleanCode.Length == 0 || cleanCode.Length > 40)
        {
            errors.Add("Enter a cohort code of up to 40 characters.");
        }

        if (cleanLocation is not null && cleanLocation.Length > 120)
        {
            errors.Add("Location must be 120 characters or fewer.");
        }

        if (errors.Count > 0)
        {
            return new CohortCreationResult(false, null, errors);
        }

        if (await _db.Cohorts.AnyAsync(cohort => cohort.Code == cleanCode))
        {
            return Failure("A cohort with this code already exists.");
        }

        var cohort = new Cohort
        {
            Name = cleanName,
            Code = cleanCode,
            LocationLabel = cleanLocation,
            IsArchived = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor.Id
        };

        _db.Cohorts.Add(cohort);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sqlException
                && (sqlException.Number == 2601 ||
                    sqlException.Number == 2627)
                && sqlException.Message.Contains("IX_Cohorts_Code"))
        {
            // Handles another request saving the same code concurrently.
            _db.Entry(cohort).State = EntityState.Detached;

            return Failure("A cohort with this code already exists.");
        }

        return new CohortCreationResult(
            true,
            cohort.CohortId,
            Array.Empty<string>());
    }

    private static CohortCreationResult Failure(string message)
    {
        return new CohortCreationResult(
            false,
            null,
            new[] { message });
    }
}