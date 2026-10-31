using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Mentor;

[Authorize(Policy = "MentorOnly")]
public class CohortModel : PageModel
{
    private readonly IMentorCohortQueryService _queryService;

    public CohortModel(IMentorCohortQueryService queryService)
    {
        _queryService = queryService;
    }

    public MentorCohortDetails Cohort { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var cohort = await _queryService.GetDetailsAsync(id, userId);

        if (cohort is null)
        {
            return NotFound();
        }

        Cohort = cohort;
        return Page();
    }
}