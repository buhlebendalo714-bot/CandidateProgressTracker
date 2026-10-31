namespace CandidateProgressTracker.Application.Cohorts;

public interface ICandidateCohortQueryService
{
    Task<IReadOnlyList<CohortSummary>> GetMyCohortsAsync(
        string candidateUserId);
}