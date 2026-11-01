using CandidateProgressTracker.Domain.Learning;

namespace CandidateProgressTracker.Application.Learning;

public record ProjectSummary(
    Guid ProjectId,
    string Title,
    string Brief,
    ProjectMode Mode,
    bool IsArchived);

public record CycleProjects(
    Guid CycleId,
    Guid CohortId,
    string CycleName,
    string CohortName,
    bool IsReadOnly,
    IReadOnlyList<ProjectSummary> Projects);

public record ProjectCreationResult(
    bool Succeeded,
    Guid? ProjectId,
    IReadOnlyList<string> Errors);

public interface IProjectService
{
    Task<CycleProjects?> GetForCycleAsync(
        Guid cycleId,
        string actorUserId);

    Task<ProjectCreationResult> CreateAsync(
        Guid cycleId,
        string title,
        string brief,
        ProjectMode mode,
        string actorUserId);
}