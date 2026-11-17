using System.Security.Claims;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin;

[Authorize(Policy = "AdministratorOnly")]
public sealed class IndexModel(ICohortQueryService cohorts, ILearningCycleService cycles, IAuditQueryService audit) : PageModel
{
    public IReadOnlyList<CohortSummary> Cohorts { get; private set; } = [];
    public CohortCycles? Selected { get; private set; }
    public IReadOnlyList<AuditRow> Recent { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid? cohortId)
    {
        Response.Headers.CacheControl = "no-store";
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var data = await cohorts.GetCohortsAsync(actor);
        if (data is null) return Forbid();
        Cohorts = data.Where(c => !c.IsArchived).ToList();
        if (cohortId is not null && !Cohorts.Any(c => c.CohortId == cohortId)) return NotFound();
        var selected = cohortId ?? Cohorts.FirstOrDefault()?.CohortId;
        if (selected is not null)
        {
            Selected = await cycles.GetForCohortAsync(selected.Value, actor);
            if (Selected is null) return NotFound();
        }
        var activity = await audit.SearchAsync(new(), actor);
        if (activity is null) return Forbid();
        Recent = activity.Rows.Take(5).ToList();
        return Page();
    }
}
