using System.Security.Claims;
using CandidateProgressTracker.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Admin;
public class AuditModel(IAuditQueryService service):PageModel
{
    [BindProperty(SupportsGet=true)]public string? Actor{get;set;}
    [BindProperty(SupportsGet=true)]public string? Action{get;set;}
    [BindProperty(SupportsGet=true)]public string? Record{get;set;}
    [BindProperty(SupportsGet=true)]public DateTimeOffset? From{get;set;}
    [BindProperty(SupportsGet=true)]public DateTimeOffset? To{get;set;}
    [BindProperty(SupportsGet=true)]public int PageNumber{get;set;}=1;
    public AuditPage Results{get;private set;}=new(1,false,[]);
    public async Task<IActionResult> OnGetAsync(){Response.Headers.CacheControl="no-store";var result=await service.SearchAsync(new(Actor,Action,Record,From,To,PageNumber),User.FindFirstValue(ClaimTypes.NameIdentifier)??"");if(result is null)return Forbid();Results=result;return Page();}
}
