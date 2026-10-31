using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;

public class MembersModel : PageModel
{
    private readonly ICohortMembershipQueryService _queryService;
    private readonly ICohortMembershipService _membershipService;

    public MembersModel(
        ICohortMembershipQueryService queryService,
        ICohortMembershipService membershipService)
    {
        _queryService = queryService;
        _membershipService = membershipService;
    }

    public CohortMembershipDetails Details { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Member and role")]
        public string Selection { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        [Display(Name = "Assignment reason")]
        public string ChangeReason { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var details = await _queryService.GetAsync(id);

        if (details is null)
        {
            return NotFound();
        }

        Details = details;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var details = await _queryService.GetAsync(id);

        if (details is null)
        {
            return NotFound();
        }

        Details = details;

        if (Details.IsArchived)
        {
            ModelState.AddModelError(
                string.Empty,
                "Members cannot be added to an archived cohort.");

            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Match the posted value against freshly loaded eligible choices.
        var selected = Details.EligibleMembers.FirstOrDefault(member =>
            $"{member.UserId}|{(byte)member.Kind}" == Input.Selection);

        if (selected is null)
        {
            ModelState.AddModelError(
                "Input.Selection",
                "This selection is no longer eligible. Choose another member.");

            return Page();
        }

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        var result = await _membershipService.AssignAsync(
            id,
            selected.UserId,
            selected.Kind,
            Input.ChangeReason,
            actorId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Member assigned successfully.";

        return RedirectToPage("./Members", new { id });
    }
}