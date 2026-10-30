using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Web.Pages.Admin.Users;

public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;

    public IndexModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public List<AccountRow> Accounts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .Take(50)
            .ToListAsync();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            Accounts.Add(new AccountRow(
                user.UserName ?? "(No username)",
                roles.Count > 0
                    ? string.Join(", ", roles.OrderBy(role => role))
                    : "No roles assigned"));
        }
    }

    public record AccountRow(string UserName, string Roles);
}