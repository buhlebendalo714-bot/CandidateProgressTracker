using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Evidence;
[Authorize]
public sealed class DownloadModel(IFileEvidenceService service) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await service.DownloadAsync(fileId, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        if (result is null) return new ContentResult {StatusCode=404,ContentType="text/plain; charset=utf-8",Content="This file is unavailable or you do not have access. Open the submission history and try again, or contact your Administrator."};
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Content, result.MediaType, result.Name);
    }
}
