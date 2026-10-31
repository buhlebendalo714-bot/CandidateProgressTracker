using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;

public class EndMembershipModel : PageModel
{
    private readonly ICohortMembershipQueryService _queryService;
    private readonly ICohortMembershipEndingService _endingService;

    public EndMembershipModel(
        ICohortMembershipQueryService queryService,
        ICohortMembershipEndingService endingService)
    {
        _queryService = queryService;
        _endingService = endingService;
    }

    public CohortMembershipDetails Details { get; private set; } = null!;
    public MembershipSummary Membership { get; private set; } = null!;

    public bool CanSubmit { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(1000)]
        [Display(Name = "Reason for ending membership")]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(
        Guid cohortId,
        Guid membershipId)
    {
        if (!await LoadAsync(cohortId, membershipId))
        {
            return NotFound();
        }

        Input.RowVersion = Convert.ToBase64String(Membership.RowVersion);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        Guid cohortId,
        Guid membershipId)
    {
        if (!await LoadAsync(cohortId, membershipId))
        {
            return NotFound();
        }

        if (!CanSubmit)
        {
            ModelState.AddModelError(
                string.Empty,
                "This membership cannot currently be ended.");

            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        byte[] expectedVersion;

        try
        {
            expectedVersion = Convert.FromBase64String(Input.RowVersion);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The membership version is invalid. Reload this page.");

            CanSubmit = false;
            return Page();
        }

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        // Use the version submitted by the form, not the freshly read version.
        var result = await _endingService.EndAsync(
            membershipId,
            Input.Reason,
            expectedVersion,
            actorId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            CanSubmit = false;
            return Page();
        }

        TempData["SuccessMessage"] = "Membership ended successfully.";

        return RedirectToPage("./Members", new { id = cohortId });
    }

    private async Task<bool> LoadAsync(
        Guid cohortId,
        Guid membershipId)
    {
        var details = await _queryService.GetAsync(cohortId);

        if (details is null)
        {
            return false;
        }

        var membership = details.Memberships
            .SingleOrDefault(item => item.MembershipId == membershipId);

        if (membership is null)
        {
            return false;
        }

        Details = details;
        Membership = membership;

        CanSubmit = !details.IsArchived
            && membership.EndsAt is null
            && membership.StartsAt < DateTimeOffset.UtcNow;

        return true;
    }
}