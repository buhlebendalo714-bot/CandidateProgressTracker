using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CandidateProgressTracker.Web.Pages.Candidate;

[Authorize(Roles = "Candidate")]
public sealed class CreditedTeamHistoryModel(ITeamSubmissionService service) : PageModel
{
    public IReadOnlyList<TeamHistoryLink> Assignments { get; private set; } = Array.Empty<TeamHistoryLink>();
    public async Task OnGetAsync() => Assignments = await service.GetCreditedHistoryAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
}
