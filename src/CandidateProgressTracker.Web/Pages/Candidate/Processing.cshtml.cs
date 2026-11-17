using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Candidate;
public sealed class ProcessingModel(IFileEvidenceService service) : PageModel
{
    public ProcessingReceipt Receipt { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid attemptId)
    {
        var result = await service.GetAsync(attemptId, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (result is null) return NotFound();
        Receipt = result; return Page();
    }
}
