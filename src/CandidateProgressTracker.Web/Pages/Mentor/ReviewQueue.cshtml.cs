using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Mentor;
public sealed class ReviewQueueModel(ILearningOverviewService service) : PageModel
{
    public ReviewQueuePage Queue {get;private set;}=null!;
    public async Task<IActionResult> OnGetAsync(Guid? cohortId,Guid? cycleId,Guid? projectId,Guid? offeringId,string status="Awaiting review",int pageNumber=1)
    {var data=await service.GetQueueAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)??"",new(cohortId,cycleId,projectId,offeringId,status,pageNumber));if(data is null)return NotFound();Queue=data;return Page();}
}
