using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Projects;

[Authorize(Policy = "AdministratorOnly")]
public class ReviseAssignmentModel : PageModel
{
    private readonly IAssignmentRevisionService _service;

    public ReviseAssignmentModel(IAssignmentRevisionService service)
    {
        _service = service;
    }

    public AssignmentRevisionDetails Details { get; private set; } = null!;

    [BindProperty]
    public RevisionInput Input { get; set; } = new();

    public class RevisionInput
    {
        [Required]
        public string RowVersion { get; set; } = "";

        [Required]
        [Display(Name = "Deadline — South African time (UTC+02:00)")]
        public string DueLocal { get; set; } = "";

        [Required, StringLength(10000)]
        public string Instructions { get; set; } = "";

        [Required, StringLength(10000)]
        [Display(Name = "Acceptance expectations")]
        public string AcceptanceExpectations { get; set; } = "";

        [Required, StringLength(2000)]
        [Display(Name = "Evidence requirements")]
        public string EvidencePolicy { get; set; } = "";

        [Required, StringLength(1000)]
        [Display(Name = "Reason for this revision")]
        public string Reason { get; set; } = "";
    }

    private string? ActorId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<bool> LoadAsync(Guid assignmentId, string actorId)
    {
        var details = await _service.GetAsync(assignmentId, actorId);

        if (details is null)
        {
            return false;
        }

        Details = details;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(Guid assignmentId)
    {
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(assignmentId, actorId))
        {
            return NotFound();
        }

        Input = new RevisionInput
        {
            RowVersion = Details.RowVersion,
            DueLocal = Details.DueAt
                .ToOffset(TimeSpan.FromHours(2))
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff",
                    CultureInfo.InvariantCulture),
            Instructions = Details.Instructions,
            AcceptanceExpectations = Details.AcceptanceExpectations,
            EvidencePolicy = Details.EvidencePolicy
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid assignmentId)
    {
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(assignmentId, actorId))
        {
            return NotFound();
        }

        if (!Details.CanManage)
        {
            ModelState.AddModelError(
                string.Empty, "This assignment is archived.");
            return Page();
        }

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"
        };

        if (!DateTime.TryParseExact(
                Input.DueLocal,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localTime) ||
            localTime.Year < 2000)
        {
            ModelState.AddModelError(
                "Input.DueLocal", "Enter a valid deadline in South African time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var dueAt = new DateTimeOffset(
            DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified),
            TimeSpan.FromHours(2));

        var result = await _service.ReviseAsync(
            assignmentId,
            Input.RowVersion,
            dueAt,
            Input.Instructions,
            Input.AcceptanceExpectations,
            Input.EvidencePolicy,
            Input.Reason,
            actorId);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Assignment revision saved.";

            return RedirectToPage(
                "./Assignments",
                new { milestoneId = Details.MilestoneId });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        // Preserve the submitted version token; never silently refresh it.
        return Page();
    }
}