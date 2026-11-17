using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Admin.Users;
public class CreateModel(IAccountLifecycleService service) : PageModel
{
    [BindProperty] public InputModel Input { get; set; }=new();
    public CredentialReceipt? Receipt { get; private set; }
    public sealed class InputModel
    {
        [Required,StringLength(256)] public string UserName {get;set;}="";
        [Required,EmailAddress,StringLength(256)] public string Email {get;set;}="";
        [Required] public string Role {get;set;}="";
    }
    public void OnGet()=>Response.Headers.CacheControl="no-store";
    public async Task<IActionResult> OnPostAsync()
    {
        Response.Headers.CacheControl="no-store";
        if(!ModelState.IsValid)return Page();
        var result=await service.CreateAsync(Input.UserName,Input.Email,Input.Role,User.FindFirstValue(ClaimTypes.NameIdentifier)??"");
        if(!result.Succeeded)ModelState.AddModelError("",result.Error!);else Receipt=result.Credential;
        return Page();
    }
}
