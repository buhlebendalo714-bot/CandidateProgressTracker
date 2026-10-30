using System.ComponentModel.DataAnnotations;
using CandidateProgressTracker.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Users;

public class CreateModel : PageModel
{
    private readonly IAccountCreationService _accountCreationService;

    public CreateModel(IAccountCreationService accountCreationService)
    {
        _accountCreationService = accountCreationService;
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
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _accountCreationService.CreateAsync(
            Input.UserName,
            Input.Password,
            Input.Role);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        TempData["SuccessMessage"] = "User account created successfully.";
        return RedirectToPage("./Index");
    }
}