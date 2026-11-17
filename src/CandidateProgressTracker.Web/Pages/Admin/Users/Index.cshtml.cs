using System.Security.Claims;
using CandidateProgressTracker.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Users;

public class IndexModel : PageModel
{
    private readonly IAccountQueryService _accountQueryService;

    public IndexModel(IAccountQueryService accountQueryService)
    {
        _accountQueryService = accountQueryService;
    }

    public IReadOnlyList<AccountSummary> Accounts { get; private set; }
        = Array.Empty<AccountSummary>();

    public async Task<IActionResult> OnGetAsync()
    {
        var accounts = await _accountQueryService.GetAccountsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (accounts is null) return Forbid();
        Accounts = accounts;
        return Page();
    }
}
