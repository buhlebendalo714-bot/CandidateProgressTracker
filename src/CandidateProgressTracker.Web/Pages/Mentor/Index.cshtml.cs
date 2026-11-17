using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Mentor;

[Authorize(Policy = "MentorOnly")]
public sealed class IndexModel(IMentorCohortQueryService cohorts, ILearningOverviewService learning) : PageModel
{
    public IReadOnlyList<CohortSummary> Cohorts { get; private set; } = [];
    public MentorOverview Overview { get; private set; } = null!;
    public IReadOnlyList<ProgressAssignment> Pending { get; private set; } = [];
    public IReadOnlyList<ProgressAssignment> Changes { get; private set; } = [];
    public bool MorePending { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? cycleId)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var overview = await learning.GetMentorAsync(actor, cycleId);
        if (overview is null) return NotFound();
        Overview = overview;
        Cohorts = await cohorts.GetMyCohortsAsync(actor);
        // Queue pages are already authorised and ordered oldest first. Continue
        // beyond unavailable/self-review rows so the primary action is eligible.
        var pending = new List<ProgressAssignment>();
        ReviewQueuePage? page;
        var number = 1;
        do
        {
            page = await learning.GetQueueAsync(actor, new(CycleId: overview.SelectedCycle, Page: number++));
            if (page is null) return NotFound();
            pending.AddRange(page.Rows.Where(r => r.CanReview));
        } while (pending.Count < 6 && page.HasNextPage);
        Pending = pending.Take(5).ToList();
        MorePending = pending.Count > 5 || page.HasNextPage;
        var changes = await learning.GetQueueAsync(actor, new(CycleId: overview.SelectedCycle, Status: "Changes requested"));
        if (changes is null) return NotFound();
        Changes = changes.Rows.Take(3).ToList();
        return Page();
    }
}
