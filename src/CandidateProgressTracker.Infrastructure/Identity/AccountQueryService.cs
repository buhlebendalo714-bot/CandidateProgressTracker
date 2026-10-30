using CandidateProgressTracker.Application.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Identity;

public class AccountQueryService : IAccountQueryService
{
    private readonly UserManager<IdentityUser> _userManager;

    public AccountQueryService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync()
    {
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
                    : "No roles assigned"));
        }

        return accounts;
    }
}