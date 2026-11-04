namespace CandidateProgressTracker.Application.Learning;

public record AssignmentCandidateOption(string UserId, string UserName);

public record AssignmentTeamOption(Guid TeamId, string Name);

public record AssignmentSummary(
    Guid AssignmentId,
    string OwnerName,
    DateTimeOffset DueAt,
    bool IsRequired);

public record MilestoneAssignmentDetails(
    Guid MilestoneId,
    Guid ProjectId,
    string ProjectTitle,
    string MilestoneTitle,
    bool IsTeamProject,
    bool CanManage,
    IReadOnlyList<AssignmentCandidateOption> Candidates,
    IReadOnlyList<AssignmentTeamOption> Teams,
    IReadOnlyList<AssignmentSummary> Assignments);

public record MilestoneAssignmentResult(
    bool Succeeded,
    IReadOnlyList<string> Errors);

public interface IMilestoneAssignmentService
{
    Task<MilestoneAssignmentDetails?> GetAsync(
        Guid milestoneId,
        string actorUserId);

    Task<MilestoneAssignmentResult> CreateAsync(
        Guid milestoneId,
        string? candidateId,
        Guid? teamId,
        DateTimeOffset dueAt,
        bool isRequired,
        string actorUserId);
}