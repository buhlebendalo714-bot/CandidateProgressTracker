namespace CandidateProgressTracker.Application.Cohorts;

public record CohortSummary(
    Guid CohortId,
    string Name,
    string Code,
    string? LocationLabel,
    bool IsArchived);

public record CohortDetails(
    Guid CohortId,
    string Name,
    string Code,
    string? LocationLabel,
    bool IsArchived,
    byte[] RowVersion);

public interface ICohortQueryService
{
    Task<IReadOnlyList<CohortSummary>> GetCohortsAsync();

    Task<CohortDetails?> GetAsync(Guid cohortId);
}