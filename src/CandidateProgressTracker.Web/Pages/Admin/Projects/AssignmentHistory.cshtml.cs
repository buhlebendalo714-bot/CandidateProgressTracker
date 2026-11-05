using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Projects;

[Authorize(Policy = "AdministratorOnly")]
public class AssignmentHistoryModel : PageModel
{
    private readonly IAssignmentHistoryQueryService _service;

    public AssignmentHistoryModel(IAssignmentHistoryQueryService service)
    {
        _service = service;
    }

    public AssignmentHistoryPage History { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        Guid assignmentId,
        int pageNumber = 1)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (pageNumber < 1 || pageNumber > 100000)
        {
            return BadRequest();
        }

        var history = await _service.GetAsync(
            assignmentId, actorId, pageNumber);

        if (history is null)
        {
            return NotFound();
        }

        History = history;
        return Page();
    }
}