namespace CandidateProgressTracker.Application.Cohorts;

public record CohortSummary(
    Guid CohortId,
    string Name,
    string Code,
    string? LocationLabel,
    bool IsArchived);

public interface ICohortQueryService
{
    Task<IReadOnlyList<CohortSummary>> GetCohortsAsync();
}