using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Candidate;

[Authorize(Roles = "Candidate")]
public sealed class SubmissionHistoryModel(IIndividualSubmissionQueryService query) : PageModel
{
    public SubmissionHistoryPage History { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid assignmentId, int pageNumber = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var history = await query.GetHistoryAsync(assignmentId, userId, pageNumber);
        if (history is null) return NotFound();
        History = history;
        return Page();
    }
}
