using System.ComponentModel.DataAnnotations;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Users;

public class CreateModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TrackerDbContext _db;

    public CreateModel(
        UserManager<IdentityUser> userManager,
        TrackerDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(256)]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 12)]
        [DataType(DataType.Password)]
        [Display(Name = "Initial password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password),
            ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Validate on the server, even if someone alters the dropdown.
        if (Input.Role != "Candidate" && Input.Role != "Mentor")
        {
            ModelState.AddModelError(
                "Input.Role",
                "Select Candidate or Mentor.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new IdentityUser
        {
            UserName = Input.UserName.Trim(),
            LockoutEnabled = true
        };

        // Save both the account and its role, or neither.
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var createResult =
            await _userManager.CreateAsync(user, Input.Password);

        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        var roleResult =
            await _userManager.AddToRoleAsync(user, Input.Role);

        if (!roleResult.Succeeded)
        {
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await transaction.CommitAsync();

        TempData["SuccessMessage"] = "User account created successfully.";
        return RedirectToPage("./Index");
    }
}