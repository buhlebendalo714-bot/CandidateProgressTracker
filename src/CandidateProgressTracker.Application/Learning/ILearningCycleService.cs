namespace CandidateProgressTracker.Application.Learning;

public record LearningCycleSummary(
    Guid CycleId,
    string Name,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsArchived);

public record CohortCycles(
    Guid CohortId,
    string CohortName,
    bool IsArchived,
    IReadOnlyList<LearningCycleSummary> Cycles);

public record LearningCycleResult(
    bool Succeeded,
    Guid? CycleId,
    IReadOnlyList<string> Errors);

public interface ILearningCycleService
{
    Task<CohortCycles?> GetForCohortAsync(
        Guid cohortId,
        string actorUserId);

    Task<LearningCycleResult> CreateAsync(
        Guid cohortId,
        string name,
        DateOnly startsOn,
        DateOnly endsOn,
        string actorUserId);
}