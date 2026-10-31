using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;

[Authorize(Policy = "AdministratorOnly")]
public class ArchiveModel : PageModel
{
    private readonly ICohortQueryService _queryService;
    private readonly ICohortArchiveService _archiveService;

    public ArchiveModel(
        ICohortQueryService queryService,
        ICohortArchiveService archiveService)
    {
        _queryService = queryService;
        _archiveService = archiveService;
    }

    public CohortDetails Cohort { get; private set; } = null!;

    public bool CanSubmit { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Enter an archive reason.")]
        [StringLength(
            1000,
            ErrorMessage = "The archive reason cannot exceed 1000 characters.")]
        [Display(Name = "Archive reason")]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var cohort = await _queryService.GetAsync(id);

        if (cohort is null)
        {
            return NotFound();
        }

        Cohort = cohort;
        CanSubmit = !cohort.IsArchived;

        Input.RowVersion = Convert.ToBase64String(cohort.RowVersion);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var cohort = await _queryService.GetAsync(id);

        if (cohort is null)
        {
            return NotFound();
        }

        Cohort = cohort;
        CanSubmit = !cohort.IsArchived;

        if (cohort.IsArchived)
        {
            ModelState.AddModelError(
                string.Empty,
                "This cohort is already archived.");

            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        byte[] expectedRowVersion;

        try
        {
            expectedRowVersion =
                Convert.FromBase64String(Input.RowVersion);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The cohort version is invalid. Reload the page.");

            CanSubmit = false;
            return Page();
        }

        if (expectedRowVersion.Length != 8)
        {
            ModelState.AddModelError(
                string.Empty,
                "The cohort version is invalid. Reload the page.");

            CanSubmit = false;
            return Page();
        }

        var actorUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Challenge();
        }

        var result = await _archiveService.ArchiveAsync(
            id,
            Input.Reason,
            expectedRowVersion,
            actorUserId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            CanSubmit = false;
            return Page();
        }

        TempData["SuccessMessage"] =
            $"Cohort '{cohort.Name}' was archived.";

        return RedirectToPage("./Index");
    }
}