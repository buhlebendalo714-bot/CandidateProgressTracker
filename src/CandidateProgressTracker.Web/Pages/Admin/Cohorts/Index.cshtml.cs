using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;

public class IndexModel : PageModel
{
    private readonly ICohortQueryService _cohortQueryService;

    public IndexModel(ICohortQueryService cohortQueryService)
    {
        _cohortQueryService = cohortQueryService;
    }

    public IReadOnlyList<CohortSummary> Cohorts { get; private set; }
        = Array.Empty<CohortSummary>();

    public async Task<IActionResult> OnGetAsync()
    {
        var cohorts = await _cohortQueryService.GetCohortsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (cohorts is null) return Forbid();
        Cohorts = cohorts;
        return Page();
    }
}
