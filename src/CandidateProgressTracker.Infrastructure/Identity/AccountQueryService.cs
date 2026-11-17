using System.Data;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Identity;

public class AccountQueryService : IAccountQueryService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TrackerDbContext _db;

    public AccountQueryService(UserManager<IdentityUser> userManager, TrackerDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IReadOnlyList<AccountSummary>?> GetAccountsAsync(string actorUserId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await AdministratorAccess.IsCurrentAsync(_db, actorUserId)) return null;
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .Take(50)
            .ToListAsync();

        var accounts = new List<AccountSummary>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            accounts.Add(new AccountSummary(
                user.UserName ?? "(No username)",
                roles.Count > 0
                    ? string.Join(", ", roles.OrderBy(role => role))
                    : "No roles assigned", user.Id));
        }

        await transaction.CommitAsync();
        return accounts;
    }
}
