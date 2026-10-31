using CandidateProgressTracker.Application.Cohorts;
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

    public async Task OnGetAsync()
    {
        Cohorts = await _cohortQueryService.GetCohortsAsync();
    }
}