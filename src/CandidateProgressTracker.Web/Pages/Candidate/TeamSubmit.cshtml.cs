using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Candidate;

[Authorize(Roles = "Candidate")]
public sealed class TeamSubmitModel(ITeamSubmissionService service, IFileEvidenceService files) : PageModel
{
    [BindProperty] public List<IFormFile> Files { get; set; } = [];
    public TeamSubmissionForm Details { get; private set; } = null!;
    [BindProperty] public TeamInput Input { get; set; } = new();
    public sealed class TeamInput
    {
        public Guid RevisionId { get; set; }
        public Guid RequestId { get; set; }
        [Required] public string RosterConfirmation { get; set; } = "";
        [Required, StringLength(10000)] public string Explanation { get; set; } = "";
        public List<EvidenceInput> Evidence { get; set; } = new();
    }
    public sealed class EvidenceInput
    {
        [StringLength(200)] public string? Label { get; set; }
        [StringLength(2000)] public string? Url { get; set; }
    }
    public async Task<IActionResult> OnGetAsync(Guid assignmentId)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var form = await service.GetFormAsync(assignmentId, actor);
        if (form is null) return NotFound();
        Details = form;
        Input = new() { RevisionId = form.Requirements.RevisionId, RosterConfirmation = form.Confirmation,
            RequestId = Guid.NewGuid(), Evidence = new List<EvidenceInput> { new() } };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid assignmentId)
    {
        TempData.Remove("TeamSubmissionSuccess");
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Challenge();
        var form = await service.GetFormAsync(assignmentId, actor);
        if (form is null) return NotFound();
        Details = form;
        Input.Evidence ??= new();
        if (Input.Evidence.Count == 0) Input.Evidence.Add(new());
        if (Input.Evidence.Count > 10) ModelState.AddModelError("", "Provide no more than 10 evidence links.");
        if (!ModelState.IsValid) return Page();
        var evidence = Input.Evidence.Where(e => !string.IsNullOrWhiteSpace(e.Label) || !string.IsNullOrWhiteSpace(e.Url))
            .Select(e => new SubmissionEvidenceInput(e.Label ?? "", e.Url ?? "")).ToList();
        if (Files.Count > 0)
        {
            if (Files.Count > FileLimits.Count || Files.Any(f => f.Length > FileLimits.PerFile) || Files.Sum(f => f.Length) > FileLimits.Total)
            { ModelState.AddModelError("", "Use at most 5 files, 10,485,760 bytes each and 52,428,800 bytes total."); return Page(); }
            var streams=Files.Select(f=>f.OpenReadStream()).ToList();
            try
            {
                var receipt=await files.ReceiveAsync(new(new(assignmentId,Input.RevisionId,Input.RequestId,Input.Explanation,evidence),Input.RosterConfirmation,
                    Files.Select((f,i)=>new EvidenceUpload(f.FileName,streams[i])).ToList()),actor,HttpContext.RequestAborted);
                if(receipt.AttemptId!=Guid.Empty) return RedirectToPage("./Processing",new {attemptId=receipt.AttemptId});
                ModelState.AddModelError("",receipt.Message);return Page();
            }
            finally {foreach(var stream in streams) await stream.DisposeAsync();}
        }
        var result = await service.SubmitAsync(new(new(assignmentId, Input.RevisionId, Input.RequestId, Input.Explanation, evidence), Input.RosterConfirmation), actor);
        if (result.Succeeded)
        {
            TempData["TeamSubmissionSuccess"] = $"Team submission received. Attempt {result.Receipt!.AttemptNo}. Receipt: {result.Receipt.SubmissionId}.";
            return RedirectToPage("./TeamSubmissionHistory", new { assignmentId });
        }
        foreach (var error in result.Errors) ModelState.AddModelError("", error);
        return Page(); // Keep the posted confirmation and request ID. A reload is explicit.
    }
}
