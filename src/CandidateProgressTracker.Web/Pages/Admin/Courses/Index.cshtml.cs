using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Admin.Courses;
public sealed class IndexModel(ICourseService service) : PageModel
{
    public CourseSetup Details { get; private set; }=null!;
    [BindProperty] public CourseInput Input {get;set;}=new();
    public sealed class CourseInput
    {
        public string Title {get;set;}=""; public string Platform {get;set;}=""; public string Url {get;set;}="";
        public Guid CourseId {get;set;} public Guid OfferingId {get;set;} public string CandidateId {get;set;}="";
        public string DueLocal {get;set;}=""; public string Instructions {get;set;}=""; public string Acceptance {get;set;}="";
        public string Evidence {get;set;}="Upload a course certificate file."; public bool Required {get;set;}=true;
    }
    private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"";
    public async Task<IActionResult> OnGetAsync(Guid cycleId)
    { var data=await service.GetSetupAsync(cycleId,Actor); if(data is null) return NotFound(); Details=data; return Page(); }
    public async Task<IActionResult> OnPostAsync(Guid cycleId,string action)
    {
        var data=await service.GetSetupAsync(cycleId,Actor); if(data is null) return NotFound(); Details=data;
        string? error;
        if(action=="create") error=await service.CreateAsync(Input.Title,Input.Platform,Input.Url,Actor);
        else if(action=="offer") error=await service.OfferAsync(Input.CourseId,cycleId,Actor);
        else if(action=="assign")
        {
            if(!Details.Offerings.Any(o=>o.CourseId==Input.OfferingId)) return NotFound();
            if(!DateTime.TryParseExact(Input.DueLocal,"yyyy-MM-ddTHH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var local)) error="Enter a valid SAST deadline.";
            else error=await service.AssignAsync(Input.OfferingId,Input.CandidateId,new DateTimeOffset(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),TimeSpan.FromHours(2)),Input.Instructions,Input.Acceptance,Input.Evidence,Input.Required,Actor);
        }
        else return BadRequest();
        if(error is null) {TempData["CourseSuccess"]=action switch {"create"=>"Course added to the catalogue.","offer"=>"Course added to this learning cycle.",_=>"Course assigned to the candidate."};return RedirectToPage(new {cycleId});}
        ModelState.AddModelError("",error); return Page();
    }
}
