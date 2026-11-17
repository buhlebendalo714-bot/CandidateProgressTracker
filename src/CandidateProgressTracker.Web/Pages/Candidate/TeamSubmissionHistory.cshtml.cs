using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Candidate;

[Authorize(Roles = "Candidate")]
public sealed class TeamSubmissionHistoryModel(ITeamSubmissionService service) : PageModel
{
    public SubmissionHistoryPage History { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid assignmentId, int pageNumber = 1)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var history = await service.GetHistoryAsync(assignmentId, actor, pageNumber);
        if (history is null) return NotFound();
        History = history;
        return Page();
    }
}
