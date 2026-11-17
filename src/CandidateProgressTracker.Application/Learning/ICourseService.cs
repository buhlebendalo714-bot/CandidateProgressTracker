namespace CandidateProgressTracker.Application.Learning;
public record CourseItem(Guid CourseId,string Title,string Platform,string Url);
public record CourseAssignmentItem(Guid AssignmentId,string Title,string Platform,string Url,string Owner,DateTimeOffset DueAt);
public record CourseSetup(IReadOnlyList<CourseItem> Courses,IReadOnlyList<CourseItem> Offerings,IReadOnlyList<CourseAssignmentItem> Assignments,IReadOnlyList<CourseCandidate> Candidates);
public record CourseCandidate(string Id,string Name);
public interface ICourseService
{
    Task<CourseSetup?> GetSetupAsync(Guid cycleId,string actor);
    Task<string?> CreateAsync(string title,string platform,string url,string actor);
    Task<string?> OfferAsync(Guid courseId,Guid cycleId,string actor);
    Task<string?> AssignAsync(Guid offeringId,string candidate,DateTimeOffset due,string instructions,string acceptance,string evidence,bool required,string actor);
    Task<IReadOnlyList<CourseAssignmentItem>> GetMineAsync(string actor,bool mentor=false);
}
