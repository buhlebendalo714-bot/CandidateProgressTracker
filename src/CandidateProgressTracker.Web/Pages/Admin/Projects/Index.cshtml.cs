using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Projects;

[Authorize(Policy = "AdministratorOnly")]
public class IndexModel : PageModel
{
    private readonly IProjectService _service;

    public IndexModel(IProjectService service)
    {
        _service = service;
    }

    public CycleProjects Cycle { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(200)]
        [Display(Name = "Project title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(10000)]
        [Display(Name = "Project brief")]
        public string Brief { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select a project mode.")]
        [EnumDataType(typeof(ProjectMode))]
        [Display(Name = "Project mode")]
        public ProjectMode? Mode { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid cycleId)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        var cycle = await _service.GetForCycleAsync(cycleId, actorId);

        if (cycle is null)
        {
            return NotFound();
        }

        Cycle = cycle;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid cycleId)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        var cycle = await _service.GetForCycleAsync(cycleId, actorId);

        if (cycle is null)
        {
            return NotFound();
        }

        Cycle = cycle;

        if (cycle.IsReadOnly)
        {
            ModelState.AddModelError(
                string.Empty,
                "You cannot create a project in an archived cycle or cohort.");

            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _service.CreateAsync(
            cycleId,
            Input.Title,
            Input.Brief,
            Input.Mode!.Value,
            actorId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Project created.";

        return RedirectToPage("./Index", new { cycleId });
    }
}