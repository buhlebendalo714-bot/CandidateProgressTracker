namespace CandidateProgressTracker.Application.Learning;

public record MilestoneSummary(
    Guid MilestoneId,
    int SequenceNo,
    string Title,
    string Instructions,
    string AcceptanceExpectations,
    string EvidencePolicy);

public record ProjectMilestones(
    Guid ProjectId,
    Guid CycleId,
    string ProjectTitle,
    bool IsReadOnly,
    IReadOnlyList<MilestoneSummary> Milestones);

public record MilestoneCreationResult(
    bool Succeeded,
    Guid? MilestoneId,
    IReadOnlyList<string> Errors);

public interface IMilestoneService
{
    Task<ProjectMilestones?> GetForProjectAsync(
        Guid projectId,
        string actorUserId);

    Task<MilestoneCreationResult> CreateAsync(
        Guid projectId,
        int sequenceNo,
        string title,
        string instructions,
        string acceptanceExpectations,
        string evidencePolicy,
        string actorUserId);
}