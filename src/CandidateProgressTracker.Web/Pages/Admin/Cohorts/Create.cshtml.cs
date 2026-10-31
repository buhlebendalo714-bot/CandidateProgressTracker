using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;

public class CreateModel : PageModel
{
    private readonly ICohortCreationService _cohortCreationService;

    public CreateModel(ICohortCreationService cohortCreationService)
    {
        _cohortCreationService = cohortCreationService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "Cohort name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(40)]
        [Display(Name = "Cohort code")]
        public string Code { get; set; } = string.Empty;

        [StringLength(120)]
        [Display(Name = "Location (optional)")]
        public string? LocationLabel { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Challenge();
        }

        var result = await _cohortCreationService.CreateAsync(
            Input.Name,
            Input.Code,
            Input.LocationLabel,
            actorUserId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Cohort created successfully.";
        return RedirectToPage("./Index");
    }
}