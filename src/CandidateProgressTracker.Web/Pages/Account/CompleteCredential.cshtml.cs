using System.ComponentModel.DataAnnotations;
using CandidateProgressTracker.Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
namespace CandidateProgressTracker.Web.Pages.Account;
[AllowAnonymous,EnableRateLimiting("Credentials")]
public class CompleteCredentialModel(IAccountLifecycleService service):PageModel
{
    [BindProperty,Required,StringLength(256)]public string UserName{get;set;}="";
    [BindProperty,Required,StringLength(100)]public string Credential{get;set;}="";
    [BindProperty,Required,StringLength(128,MinimumLength=12),DataType(DataType.Password)]public string Password{get;set;}="";
    [BindProperty,Compare(nameof(Password)),DataType(DataType.Password)]public string ConfirmPassword{get;set;}="";
    public bool Completed{get;private set;}
    public void OnGet()=>Response.Headers.CacheControl="no-store";
    public async Task<IActionResult> OnPostAsync()
    {
        Response.Headers.CacheControl="no-store";
        if(!ModelState.IsValid)return Page();
        var result=await service.RedeemAsync(UserName,Credential,Password);
        if(!result.Succeeded)ModelState.AddModelError("",result.Error!);else Completed=true;
        // Never redisplay secrets, including in validation responses.
        ModelState.Remove(nameof(Credential));ModelState.Remove(nameof(Password));ModelState.Remove(nameof(ConfirmPassword));
        Credential=Password=ConfirmPassword="";return Page();
    }
}
