using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Candidate;
[Authorize(Policy="CandidateOnly")]
public sealed class IndexModel(ILearningOverviewService overview, ICandidateCohortQueryService cohorts):PageModel
{
    public CandidateOverview Overview {get;private set;}=null!;
    public IReadOnlyList<CohortSummary> Cohorts {get;private set;}=[];
    public IReadOnlyList<ProgressAssignment> Actionable {get;private set;}=[];
    public ProgressAssignment? NextAction=>Actionable.FirstOrDefault();
    public DateTimeOffset Now {get;private set;}
    public async Task<IActionResult> OnGetAsync(Guid? cycleId)
    {
        var actor=User.FindFirstValue(ClaimTypes.NameIdentifier);if(string.IsNullOrWhiteSpace(actor))return Challenge();
        var data=await overview.GetCandidateAsync(actor,cycleId);if(data is null)return NotFound();
        Overview=data;Cohorts=await cohorts.GetMyCohortsAsync(actor);Now=DateTimeOffset.UtcNow;
        Actionable=data.Assignments.Where(a=>a.CountsForProgress && a.CanSubmit && a.NeedsCandidateAction)
            .OrderBy(a=>a.Status=="Changes requested"?0:a.DueAt<Now?1:2).ThenBy(a=>a.DueAt).ThenBy(a=>a.AssignmentId).ToList();
        return Page();
    }
}
