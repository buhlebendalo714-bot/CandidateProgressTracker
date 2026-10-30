using CandidateProgressTracker.Application.Accounts;
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

    public async Task OnGetAsync()
    {
        Accounts = await _accountQueryService.GetAccountsAsync();
    }
}