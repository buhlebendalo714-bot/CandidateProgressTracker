using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;

[Authorize(Policy = "AdministratorOnly")]
public class CyclesModel : PageModel
{
    private readonly ILearningCycleService _service;

    public CyclesModel(ILearningCycleService service)
    {
        _service = service;
    }

    public CohortCycles Cohort { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "Cycle name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a start date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateOnly? StartsOn { get; set; }

        [Required(ErrorMessage = "Enter an end date.")]
        [DataType(DataType.Date)]
        [Display(Name = "End date")]
        public DateOnly? EndsOn { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        var cohort = await _service.GetForCohortAsync(id, actorId);

        if (cohort is null)
        {
            return NotFound();
        }

        Cohort = cohort;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        var cohort = await _service.GetForCohortAsync(id, actorId);

        if (cohort is null)
        {
            return NotFound();
        }

        Cohort = cohort;

        if (cohort.IsArchived)
        {
            ModelState.AddModelError(
                string.Empty,
                "You cannot create a cycle in an archived cohort.");

            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _service.CreateAsync(
            id,
            Input.Name,
            Input.StartsOn!.Value,
            Input.EndsOn!.Value,
            actorId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Learning cycle created.";

        return RedirectToPage("./Cycles", new { id });
    }
}