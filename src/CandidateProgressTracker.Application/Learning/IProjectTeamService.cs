namespace CandidateProgressTracker.Application.Learning;

public record TeamMemberSummary(
    string UserId,
    string UserName,
    Guid TeamMembershipId = default,
    string RowVersion = "");

public record TeamMembershipHistory(
    string TeamName,
    string UserName,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string ChangeReason,
    string ChangedBy,
    string? EndReason,
    string? EndedBy);

public record ProjectTeamSummary(
    Guid TeamId,
    string Name,
    IReadOnlyList<TeamMemberSummary> Members);

public record ProjectTeamDetails(
    Guid ProjectId,
    Guid CycleId,
    string ProjectTitle,
    bool CanManage,
    IReadOnlyList<ProjectTeamSummary> Teams,
    IReadOnlyList<TeamMemberSummary> EligibleCandidates)
{
    public IReadOnlyList<TeamMembershipHistory> History { get; init; }
        = Array.Empty<TeamMembershipHistory>();
}

public record ProjectTeamResult(
    bool Succeeded,
    IReadOnlyList<string> Errors);

public interface IProjectTeamService
{
    Task<ProjectTeamDetails?> GetAsync(
        Guid projectId,
        string actorUserId);

    Task<ProjectTeamResult> CreateAsync(
        Guid projectId,
        string name,
        string actorUserId);

    Task<ProjectTeamResult> AssignAsync(
        Guid projectId,
        Guid teamId,
        string candidateId,
        string reason,
        string actorUserId);

    Task<ProjectTeamResult> EndAsync(
        Guid projectId,
        Guid membershipId,
        string rowVersion,
        string reason,
        string actorUserId);
}