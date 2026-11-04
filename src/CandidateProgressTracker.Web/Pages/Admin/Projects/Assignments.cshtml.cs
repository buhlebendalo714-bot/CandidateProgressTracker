using System.Globalization;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Projects;

[Authorize(Policy = "AdministratorOnly")]
public class AssignmentsModel : PageModel
{
    private readonly IMilestoneAssignmentService _service;

    public AssignmentsModel(IMilestoneAssignmentService service)
    {
        _service = service;
    }

    public MilestoneAssignmentDetails Details { get; private set; } = null!;

    [BindProperty]
    public string? CandidateId { get; set; }

    [BindProperty]
    public Guid? TeamId { get; set; }

    [BindProperty]
    public string? DueLocal { get; set; }

    [BindProperty]
    public bool IsRequired { get; set; } = true;

    private string? ActorId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<bool> LoadAsync(Guid milestoneId, string actorId)
    {
        var details = await _service.GetAsync(milestoneId, actorId);

        if (details is null)
        {
            return false;
        }

        Details = details;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(Guid milestoneId)
    {
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(milestoneId, actorId))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid milestoneId)
    {
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(milestoneId, actorId))
        {
            return NotFound();
        }

        if (!Details.CanManage)
        {
            ModelState.AddModelError(
                string.Empty,
                "Assignments cannot be created for this archived or unsupported project.");

            return Page();
        }

        if (Details.IsTeamProject)
        {
            if (!TeamId.HasValue || TeamId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(TeamId), "Select a team.");
            }
        }
        else if (string.IsNullOrWhiteSpace(CandidateId))
        {
            ModelState.AddModelError(nameof(CandidateId), "Select a candidate.");
        }

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss"
        };

        var validDate = DateTime.TryParseExact(
            DueLocal,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var localTime);

        if (!validDate || localTime.Year < 2000)
        {
            ModelState.AddModelError(
                nameof(DueLocal),
                "Enter a valid deadline in South African time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // SAST is UTC+02:00. Never use the server's local timezone.
        var dueAt = new DateTimeOffset(
            DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified),
            TimeSpan.FromHours(2));

        var result = await _service.CreateAsync(
            milestoneId,
            CandidateId,
            TeamId,
            dueAt,
            IsRequired,
            actorId);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Assignment created.";
            return RedirectToPage("./Assignments", new { milestoneId });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (!await LoadAsync(milestoneId, actorId))
        {
            return NotFound();
        }

        return Page();
    }
}