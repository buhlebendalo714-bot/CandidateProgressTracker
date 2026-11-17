using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Admin;
public class ArchivesModel(IArchivedHistoryService service):PageModel
{
    [BindProperty(SupportsGet=true)]public Guid? ScopeId{get;set;}
    [BindProperty(SupportsGet=true)]public string Kind{get;set;}="Cycle";
    [BindProperty]public string Version{get;set;}="";
    [BindProperty]public string Reason{get;set;}="";
    [BindProperty]public bool Archive{get;set;}
    [BindProperty]public Guid RequestId{get;set;}=Guid.NewGuid();
    public IReadOnlyList<ArchiveScope> Scopes{get;private set;}=[];
    public ArchiveDetails? Details{get;private set;}
    private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"";
    public async Task<IActionResult> OnGetAsync(){var result=await service.GetScopesAsync(Actor);if(result is null)return Forbid();Scopes=result;if(ScopeId is {} id){Details=await service.GetAsync(id,Kind,Actor);if(Details is null)return NotFound();Version=Details.Scope.Version;Archive=!Details.Scope.Archived;}return Page();}
    public async Task<IActionResult> OnPostAsync(){var error=await service.ChangeAsync(new(ScopeId??Guid.Empty,Kind,Version,Archive,Reason,RequestId),Actor);if(error is null){TempData["ArchiveSuccess"]=Archive?"Work archived. History and evidence are retained.":"Work reopened. Existing approvals are unchanged.";return RedirectToPage(new{ScopeId,Kind});}ModelState.AddModelError("",error);Scopes=await service.GetScopesAsync(Actor)??[];Details=await service.GetAsync(ScopeId??Guid.Empty,Kind,Actor);return Page();}
}
