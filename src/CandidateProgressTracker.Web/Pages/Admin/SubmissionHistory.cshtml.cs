using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Admin;
public class SubmissionHistoryModel(IArchivedHistoryService service):PageModel
{
    public SubmissionHistoryPage History{get;private set;}=null!;
    public async Task<IActionResult> OnGetAsync(Guid assignmentId,int pageNumber=1){var h=await service.HistoryAsync(assignmentId,User.FindFirstValue(ClaimTypes.NameIdentifier)??"",pageNumber);if(h is null)return NotFound();History=h;return Page();}
}
