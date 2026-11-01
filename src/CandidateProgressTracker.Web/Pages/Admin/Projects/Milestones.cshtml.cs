using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Projects;

[Authorize(Policy = "AdministratorOnly")]
public class MilestonesModel : PageModel
{
    private readonly IMilestoneService _service;

    public MilestonesModel(IMilestoneService service)
    {
        _service = service;
    }

    public ProjectMilestones Project { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Range(1, int.MaxValue)]
        [Display(Name = "Sequence number")]
        public int SequenceNo { get; set; } = 1;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(10000)]
        public string Instructions { get; set; } = string.Empty;

        [Required]
        [StringLength(10000)]
        [Display(Name = "Acceptance expectations")]
        public string AcceptanceExpectations { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        [Display(Name = "Evidence requirements")]
        public string EvidencePolicy { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid projectId)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        var project = await _service.GetForProjectAsync(projectId, actorId);

        if (project is null)
        {
            return NotFound();
        }

        Project = project;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid projectId)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        var project = await _service.GetForProjectAsync(projectId, actorId);

        if (project is null)
        {
            return NotFound();
        }

        Project = project;

        if (project.IsReadOnly)
        {
            ModelState.AddModelError(
                string.Empty,
                "You cannot create a milestone in an archived project, cycle, or cohort.");

            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _service.CreateAsync(
            projectId,
            Input.SequenceNo,
            Input.Title,
            Input.Instructions,
            Input.AcceptanceExpectations,
            Input.EvidencePolicy,
            actorId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Milestone created.";

        return RedirectToPage("./Milestones", new { projectId });
    }
}