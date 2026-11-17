using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Mentor;

[Authorize(Roles = "Mentor")]
public sealed class ReviewSubmissionModel(IIndividualSubmissionReviewService service) : PageModel
{
    public ReviewPage Details { get; private set; } = null!;
    [BindProperty] public ReviewInput Input { get; set; } = new();
    public sealed class ReviewInput
    {
        public Guid SubmissionId { get; set; }
        public Guid RequestId { get; set; }
        [Required] public string RowVersion { get; set; } = "";
        public ReviewDecision Decision { get; set; }
        [StringLength(ReviewLimits.Feedback)] public string? Feedback { get; set; }
    }
    public async Task<IActionResult> OnGetAsync(Guid assignmentId)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var details = await service.GetReviewAsync(assignmentId, actor);
        if (details is null) return NotFound();
        Details = details;
        Input = new() { SubmissionId = details.Attempt?.ReceiptId ?? Guid.Empty,
            RowVersion = details.RowVersion, RequestId = Guid.NewGuid() };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid assignmentId)
    {
        TempData.Remove("ReviewSuccess");
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var details = await service.GetReviewAsync(assignmentId, actor);
        if (details is null) return NotFound();
        Details = details;
        if (!ModelState.IsValid) return Page();
        var result = await service.DecideAsync(new(assignmentId, Input.SubmissionId, Input.RowVersion,
            Input.RequestId, Input.Decision, Input.Feedback ?? ""), actor);
        if (result.Succeeded)
        {
            TempData["ReviewSuccess"] = Input.Decision == ReviewDecision.Approved ? "Submission approved. The candidate can see your decision and feedback." : "Changes requested. The candidate can read your feedback and submit another attempt.";
            return RedirectToPage(new { assignmentId });
        }
        foreach (var error in result.Errors) ModelState.AddModelError("", error);
        if (result.Conflict) Response.StatusCode = StatusCodes.Status409Conflict;
        return Page();
    }
}
