using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Shared;

public abstract class MemberAssignmentsPageModel : PageModel
{
    private readonly IMemberAssignmentQueryService _service;
    private readonly MembershipKind _membershipKind;

    protected MemberAssignmentsPageModel(
        IMemberAssignmentQueryService service,
        MembershipKind membershipKind)
    {
        _service = service;
        _membershipKind = membershipKind;
    }

    public MemberAssignmentPage Assignments { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (pageNumber < 1 || pageNumber > 100000)
        {
            return BadRequest();
        }

        var result = await _service.GetAsync(
            userId, _membershipKind, pageNumber);

        if (result is null)
        {
            return Forbid();
        }

        Assignments = result;
        return Page();
    }
}