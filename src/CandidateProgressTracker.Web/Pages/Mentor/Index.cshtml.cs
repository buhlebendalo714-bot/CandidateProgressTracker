using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Mentor;

[Authorize(Policy = "MentorOnly")]
public class IndexModel : PageModel
{
    private readonly IMentorCohortQueryService _queryService;

    public IndexModel(IMentorCohortQueryService queryService)
    {
        _queryService = queryService;
    }

    public IReadOnlyList<CohortSummary> Cohorts { get; private set; }
        = Array.Empty<CohortSummary>();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        Cohorts = await _queryService.GetMyCohortsAsync(userId);

        return Page();
    }
}