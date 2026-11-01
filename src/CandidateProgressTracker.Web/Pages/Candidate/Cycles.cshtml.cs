using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Candidate;

[Authorize(Policy = "CandidateOnly")]
public class CyclesModel : PageModel
{
    private readonly IMemberCycleQueryService _query;

    public CyclesModel(IMemberCycleQueryService query)
    {
        _query = query;
    }

    public CohortCycles Cohort { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var cohort = await _query.GetAsync(
            id, userId, MembershipKind.Candidate);

        if (cohort is null)
        {
            return NotFound();
        }

        Cohort = cohort;
        return Page();
    }
}