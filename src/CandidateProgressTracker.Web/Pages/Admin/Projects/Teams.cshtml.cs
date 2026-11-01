using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Admin.Projects;

[Authorize(Policy = "AdministratorOnly")]
public class TeamsModel : PageModel
{
    private readonly IProjectTeamService _service;

    public TeamsModel(IProjectTeamService service)
    {
        _service = service;
    }

    public ProjectTeamDetails Project { get; private set; } = null!;

    public string TeamName { get; private set; } = string.Empty;
    public Guid SelectedTeamId { get; private set; }
    public string CandidateId { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;

    private string? ActorId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<bool> LoadAsync(Guid projectId, string actorId)
    {
        var project = await _service.GetAsync(projectId, actorId);

        if (project is null)
        {
            return false;
        }

        Project = project;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(Guid projectId)
    {
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(projectId, actorId))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(
        Guid projectId,
        string? teamName)
    {
        TeamName = teamName ?? string.Empty;
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(projectId, actorId))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _service.CreateAsync(
            projectId, TeamName, actorId);

        return await FinishAsync(
            projectId, actorId, result, "Team created.");
    }

    public async Task<IActionResult> OnPostAssignAsync(
        Guid projectId,
        Guid teamId,
        string? candidateId,
        string? reason)
    {
        SelectedTeamId = teamId;
        CandidateId = candidateId ?? string.Empty;
        Reason = reason ?? string.Empty;
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(projectId, actorId))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _service.AssignAsync(
            projectId, teamId, CandidateId, Reason, actorId);

        return await FinishAsync(
            projectId, actorId, result, "Candidate assigned to team.");
    }

    private async Task<IActionResult> FinishAsync(
        Guid projectId,
        string actorId,
        ProjectTeamResult result,
        string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = successMessage;
            return RedirectToPage("./Teams", new { projectId });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (!await LoadAsync(projectId, actorId))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostEndAsync(
        Guid projectId,
        Guid membershipId,
        string? rowVersion,
        string? endReason)
    {
        var actorId = ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (!await LoadAsync(projectId, actorId))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _service.EndAsync(
            projectId,
            membershipId,
            rowVersion ?? string.Empty,
            endReason ?? string.Empty,
            actorId);

        return await FinishAsync(
            projectId, actorId, result, "Team membership ended.");
    }
}