using System.Security.Claims;
using CandidateProgressTracker.Application.Learning;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Candidate;
public sealed class CoursesModel(ICourseService service) : PageModel
{
    public IReadOnlyList<CourseAssignmentItem> Courses { get; private set; } = [];
    public async Task OnGetAsync() => Courses = await service.GetMineAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
}
