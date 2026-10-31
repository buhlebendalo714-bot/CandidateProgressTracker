namespace CandidateProgressTracker.Application.Cohorts;

public record CohortCreationResult(
    bool Succeeded,
    Guid? CohortId,
    IReadOnlyList<string> Errors);

public interface ICohortCreationService
{
    Task<CohortCreationResult> CreateAsync(
        string name,
        string code,
        string? locationLabel,
        string actorUserId);
}