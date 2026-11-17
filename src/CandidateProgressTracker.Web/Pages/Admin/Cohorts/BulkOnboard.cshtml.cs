using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;

[RequestSizeLimit(2_097_152)]
[RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]
public class BulkOnboardModel(ICohortMembershipQueryService cohorts, IBulkOnboardingPreviewService previews, IBulkOnboardingImportService imports, IDataProtectionProvider protection) : PageModel
{
    public CohortMembershipDetails Details { get; private set; } = null!;
    public IReadOnlyList<OnboardingSavedBatch> SavedBatches { get; private set; } = [];
    public BulkOnboardingPreview? Preview { get; private set; }
    [BindProperty, Required, DataType(DataType.Date), Display(Name = "Membership start date (SAST)")]
    public DateOnly? StartDate { get; set; }
    [BindProperty, Required, Display(Name = "Candidate CSV file")]
    public IFormFile? CsvFile { get; set; }
    [BindProperty] public string? ConfirmationTicket { get; set; }
    private ITimeLimitedDataProtector Protector => protection.CreateProtector("BulkOnboarding.Confirmation.v1").ToTimeLimitedDataProtector();
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private async Task<bool> LoadAsync(Guid id)
    {
        Response.Headers.CacheControl = "no-store";
        var details = await cohorts.GetAsync(id, Actor);
        if (details is null || details.IsArchived) return false;
        Details = details; SavedBatches = await imports.ListAsync(id, Actor); return true;
    }
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await LoadAsync(id)) return NotFound();
        StartDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).DateTime);
        return Page();
    }
    public async Task<IActionResult> OnGetTemplateAsync(Guid id)
    {
        if (!await LoadAsync(id)) return NotFound();
        // Static headers only: no uploaded/account values or spreadsheet formulas exported.
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("UserName,Email\r\n")).ToArray(),
            "text/csv; charset=utf-8", "candidate-onboarding-template.csv");
    }
    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (CsvFile is not null && (CsvFile.Length == 0 || CsvFile.Length > IBulkOnboardingPreviewService.MaximumBytes))
            ModelState.AddModelError(nameof(CsvFile), "Choose a CSV file with at least one candidate, up to 1 MB.");
        if (!ModelState.IsValid) return Page();
        using var bytes = new MemoryStream();
        await CsvFile!.CopyToAsync(bytes, HttpContext.RequestAborted);
        Preview = await previews.PreviewAsync(id, StartDate!.Value, bytes.ToArray(), Actor);
        if (Preview is null) return NotFound();
        if (Preview.Issues.Count == 0 && Preview.Rows.Count > 0 && Preview.Rows.All(r => r.Action != OnboardingAction.Conflict) && await imports.IsAvailableAsync(Actor))
            ConfirmationTicket = Protector.Protect(JsonSerializer.Serialize(new OnboardingConfirmation(Guid.NewGuid(), id, StartDate.Value, bytes.ToArray(), OnboardingFingerprint.Of(Preview), Actor)), TimeSpan.FromMinutes(30));
        return Page();
    }
    public async Task<IActionResult> OnPostConfirmAsync(Guid id)
    {
        if (!await LoadAsync(id)) return NotFound();
        ModelState.Clear();
        OnboardingConfirmation? request;
        try { request = JsonSerializer.Deserialize<OnboardingConfirmation>(Protector.Unprotect(ConfirmationTicket ?? "")); }
        catch (Exception e) when (e is CryptographicException or JsonException or ArgumentException or FormatException)
        { ModelState.AddModelError("", "The preview expired or is invalid. Upload and review the CSV again."); return Page(); }
        if (request is null || request.CohortId != id || request.Actor != Actor) return NotFound();
        var result = await imports.ConfirmAsync(request, Actor);
        if (result is null) return NotFound();
        if (result.Rows.Count == 0) { ModelState.AddModelError("", result.Message!); return Page(); }
        return RedirectToPage("./OnboardingBatch", new { id = result.BatchId });
    }

}
