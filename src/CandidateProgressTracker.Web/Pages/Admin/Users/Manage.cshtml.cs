using System.Security.Claims;
using CandidateProgressTracker.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Admin.Users;
public class ManageModel(IAccountLifecycleService service):PageModel
{
    [BindProperty(SupportsGet=true)]public string UserId{get;set;}="";
    [BindProperty]public string Version{get;set;}="";
    [BindProperty]public string Email{get;set;}="";
    [BindProperty]public string[] Roles{get;set;}=[];
    [BindProperty]public string Reason{get;set;}="";
    [BindProperty]public string Operation{get;set;}="";
    public ManagedAccount? Account{get;private set;}
    public CredentialReceipt? Receipt{get;private set;}
    public string? Message{get;private set;}
    private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"";
    public async Task<IActionResult> OnGetAsync(){Response.Headers.CacheControl="no-store";Account=await service.GetAsync(UserId,Actor);if(Account is null)return NotFound();Version=Account.Version;Email=Account.Email??"";Roles=Account.Roles.Split(", ");return Page();}
    public async Task<IActionResult> OnPostAsync()
    {
        Response.Headers.CacheControl="no-store";
        var result=Operation switch{
            "Update"=>await service.UpdateAsync(UserId,Email,Roles,Version,Reason,Actor),
            "Deactivate"=>await service.DeactivateAsync(UserId,Version,Reason,Actor),
            "Activation" or "Recovery"=>await service.IssueAsync(UserId,Operation,Version,Reason,Actor),
            _=>new AccountOperation(false,"Select an operation.")};
        Account=await service.GetAsync(UserId,Actor);if(Account is null)return NotFound();
        if(!result.Succeeded)ModelState.AddModelError("",result.Error!);else{Receipt=result.Credential;Message=Operation switch {
            "Recovery"=>"Password reset code created.",
            "Activation"=>"Activation code created.",
            "Deactivate"=>"Account deactivated. Login and existing sessions are blocked.",
            _=>"Account details saved. The account holder must log in again; earlier codes no longer work."};}
        Version=Account.Version;ModelState.Remove(nameof(Version));return Page();
    }
}
