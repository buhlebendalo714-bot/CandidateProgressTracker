using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Account;

[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Credentials")]
public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public LoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);

            return LocalRedirect(await GetDestinationAsync(user));
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userName = Input.UserName.Trim();

        var result = await _signInManager.PasswordSignInAsync(
            userName,
            Input.Password,
            isPersistent: false,
            lockoutOnFailure: true);
        HttpContext.RequestServices.GetRequiredService<ILogger<LoginModel>>().LogInformation(
            "Authentication {Outcome} correlation {CorrelationId}",result.Succeeded?"succeeded":"denied",HttpContext.TraceIdentifier);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByNameAsync(userName);

            return LocalRedirect(await GetDestinationAsync(user));
        }

        ModelState.AddModelError(
            string.Empty,
            "Unable to log in. Check your details or contact your administrator.");

        return Page();
    }

    private async Task<string> GetDestinationAsync(IdentityUser? user)
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
            Url.IsLocalUrl(ReturnUrl))
        {
            return ReturnUrl;
        }

        if (user is null)
        {
            return Url.Content("~/");
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains("Administrator"))
        {
            return Url.Content("~/Admin");
        }

        if (roles.Contains("Mentor"))
        {
            return Url.Content("~/Mentor");
        }

        if (roles.Contains("Candidate"))
        {
            return Url.Content("~/Candidate");
        }

        return Url.Content("~/");
    }
}
