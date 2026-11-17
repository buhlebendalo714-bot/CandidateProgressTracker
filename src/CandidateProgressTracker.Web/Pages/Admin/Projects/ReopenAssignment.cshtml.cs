using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Projects;

[Authorize(Policy = "AdministratorOnly")]
public sealed class ReopenAssignmentModel(IAssignmentReopeningService service) : PageModel
{
    public ReviewPage Details { get; private set; } = null!;
    [BindProperty] public ReopenInput Input { get; set; } = new();
    public sealed class ReopenInput
    {
        public Guid ApprovalReviewId { get; set; }
        public Guid RequestId { get; set; }
        [Required] public string RowVersion { get; set; } = "";
        [Required, StringLength(ReviewLimits.ReopeningReason)] public string Reason { get; set; } = "";
    }
    public async Task<IActionResult> OnGetAsync(Guid assignmentId)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var details = await service.GetReopeningAsync(assignmentId, actor);
        if (details is null) return NotFound();
        Details = details;
        Input = new() { ApprovalReviewId = details.EffectiveApprovalId ?? Guid.Empty,
            RowVersion = details.RowVersion, RequestId = Guid.NewGuid() };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid assignmentId)
    {
        TempData.Remove("ReopenSuccess");
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var details = await service.GetReopeningAsync(assignmentId, actor);
        if (details is null) return NotFound();
        Details = details;
        if (!ModelState.IsValid) return Page();
        var result = await service.ReopenAsync(new(assignmentId, Input.ApprovalReviewId, Input.RowVersion,
            Input.RequestId, Input.Reason), actor);
        if (result.Succeeded)
        {
            TempData["ReopenSuccess"] = "Assignment approval reopened. Eligible candidates can submit another attempt.";
            return RedirectToPage(new { assignmentId });
        }
        foreach (var error in result.Errors) ModelState.AddModelError("", error);
        if (result.Conflict) Response.StatusCode = StatusCodes.Status409Conflict;
        return Page();
    }
}
