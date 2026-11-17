using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Candidate;
public sealed class ProgressModel(ILearningOverviewService service) : PageModel
{
    public CandidateOverview Overview {get;private set;}=null!;
    public async Task<IActionResult> OnGetAsync(Guid? cycleId)
    {var data=await service.GetCandidateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)??"",cycleId);if(data is null) return NotFound();Overview=data;return Page();}
}
