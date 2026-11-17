using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Candidate;

[Authorize(Roles = "Candidate")]
public sealed class SubmitModel : PageModel
{
    private readonly IIndividualSubmissionQueryService _query;
    private readonly IIndividualSubmissionService _submissions;
    private readonly IFileEvidenceService _files;
    [BindProperty] public List<IFormFile> Files { get; set; } = [];

    public SubmitModel(
        IIndividualSubmissionQueryService query,
        IIndividualSubmissionService submissions, IFileEvidenceService files)
    {
        _query = query;
        _submissions = submissions;
        _files = files;
    }

    public IndividualSubmissionDetails Details { get; private set; } = null!;

    public bool RequirementsChanged { get; private set; }

    [BindProperty]
    public SubmissionInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid assignmentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var details = await _query.GetAsync(assignmentId, userId);

        if (details is null)
        {
            return NotFound();
        }

        Details = details;

        Input = new SubmissionInput
        {
            RevisionId = details.RevisionId,
            RequestId = Guid.NewGuid(),
            Evidence = new List<EvidenceInput> { new() }
        };

        return Page();
    }

        public async Task<IActionResult> OnPostAsync(Guid assignmentId)
        {
            // A previous confirmation must not hide this request's errors.
            TempData.Remove("SubmissionSuccess");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var details = await _query.GetAsync(assignmentId, userId);

        if (details is null)
        {
            return NotFound();
        }

        Details = details;
        RequirementsChanged = Input.RevisionId != details.RevisionId;

        if (Input.RevisionId == Guid.Empty || Input.RequestId == Guid.Empty)
        {
            ModelState.AddModelError(
                string.Empty,
                "The submission form is invalid. Reload this page.");
        }

        var rows = Input.Evidence ?? new List<EvidenceInput>();

        if (rows.Count > 10)
        {
            ModelState.AddModelError(
                string.Empty,
                "Provide no more than 10 evidence links.");
        }

        // Ignore unused rows, but keep partially completed rows so that
        // the submission service can reject a missing label or URL.
        var evidence = rows
            .Where(row =>
                !string.IsNullOrWhiteSpace(row.Label) ||
                !string.IsNullOrWhiteSpace(row.Url))
            .Select(row => new SubmissionEvidenceInput(
                row.Label ?? string.Empty,
                row.Url ?? string.Empty))
            .ToList();

        if (evidence.Count == 0 && Files.Count == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Provide at least one evidence link.");
        }

        if (!ModelState.IsValid)
        {
            RestoreEvidenceRows();
            return Page();
        }

        if (Files.Count > 0)
        {
            if (Files.Count > FileLimits.Count || Files.Any(f => f.Length > FileLimits.PerFile) || Files.Sum(f => f.Length) > FileLimits.Total)
            { ModelState.AddModelError("", "Use at most 5 files, 10,485,760 bytes each and 52,428,800 bytes total."); RestoreEvidenceRows(); return Page(); }
            var streams = Files.Select(f => f.OpenReadStream()).ToList();
            try
            {
                var processingReceipt = await _files.ReceiveAsync(new(new(assignmentId, Input.RevisionId, Input.RequestId, Input.Explanation, evidence), "",
                    Files.Select((f, i) => new EvidenceUpload(f.FileName, streams[i])).ToList()), userId, HttpContext.RequestAborted);
                if (processingReceipt.AttemptId != Guid.Empty) return RedirectToPage("./Processing", new { attemptId = processingReceipt.AttemptId });
                ModelState.AddModelError("", processingReceipt.Message); RestoreEvidenceRows(); return Page();
            }
            finally { foreach (var stream in streams) await stream.DisposeAsync(); }
        }
        // Keep the posted revision and request ID unchanged.
        // The service distinguishes retries from new submissions.
        var result = await _submissions.SubmitAsync(
            new IndividualSubmissionRequest(
                assignmentId,
                Input.RevisionId,
                Input.RequestId,
                Input.Explanation,
                evidence),
            userId);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            RestoreEvidenceRows();
            return Page();
        }

        var receipt = result.Receipt!;

        TempData["SubmissionSuccess"] =
            $"Submission received. Attempt {receipt.AttemptNo}. " +
            $"Receipt: {receipt.SubmissionId}. " +
            $"Received at " +
            $"{receipt.ReceivedAt.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm:ss} SAST.";

        return RedirectToPage(
            "./Submit",
            new { assignmentId });
    }

    private void RestoreEvidenceRows()
    {
        Input.Evidence ??= new List<EvidenceInput>();

        while (Input.Evidence.Count < 1)
        {
            Input.Evidence.Add(new EvidenceInput());
        }
    }

    public sealed class SubmissionInput
    {
        public Guid RevisionId { get; set; }

        public Guid RequestId { get; set; }

        [Required]
        [StringLength(10000)]
        public string Explanation { get; set; } = string.Empty;

        public List<EvidenceInput> Evidence { get; set; } = new();
    }

    public sealed class EvidenceInput
    {
        [StringLength(200)]
        public string? Label { get; set; }

        [StringLength(2000)]
        public string? Url { get; set; }
    }
}