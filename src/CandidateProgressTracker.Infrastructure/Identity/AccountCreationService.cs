using System.Data;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Identity;

public class AccountCreationService : IAccountCreationService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TrackerDbContext _db;

    public AccountCreationService(
        UserManager<IdentityUser> userManager,
        TrackerDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<AccountCreationResult> CreateAsync(
        string userName,
        string password,
        string role,
        string actorUserId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 256)
        {
            errors.Add("Enter a username of up to 256 characters.");
        }

        if (string.IsNullOrEmpty(password) ||
            password.Length < 12 ||
            password.Length > 128)
        {
            errors.Add("The password must contain 12 to 128 characters.");
        }

        if (role != "Candidate" && role != "Mentor")
        {
            errors.Add("Select Candidate or Mentor.");
        }

        if (errors.Count > 0)
        {
            return new AccountCreationResult(false, errors);
        }

        var user = new IdentityUser
        {
            UserName = userName.Trim(),
            LockoutEnabled = true
        };

        await using var transaction =
            await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        if (!await AdministratorAccess.IsCurrentAsync(_db, actorUserId))
            return new(false, new[] { "Current Administrator access is required." });

        var createResult =
            await _userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            return new AccountCreationResult(
                false,
                createResult.Errors
                    .Select(error => error.Description)
                    .ToArray());
        }

        var roleResult =
            await _userManager.AddToRoleAsync(user, role);

        if (!roleResult.Succeeded)
        {
            return new AccountCreationResult(
                false,
                roleResult.Errors
                    .Select(error => error.Description)
                    .ToArray());
        }

        // Compatibility contract creates a pending account, never an active
        // administrator-chosen-password bypass. The UI uses CreateAsync on the
        // lifecycle service with a unique email and administrator-issued activation.
        _db.AccountAccessStates.Add(new() { UserId=user.Id,Enabled=false,
            ChangedBy=actorUserId,ChangedAt=DateTimeOffset.UtcNow,Reason="Activation required" });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new AccountCreationResult(
            true,
            Array.Empty<string>());
    }
}
