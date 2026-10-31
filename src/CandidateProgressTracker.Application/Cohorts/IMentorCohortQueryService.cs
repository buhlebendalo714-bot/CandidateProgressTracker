namespace CandidateProgressTracker.Application.Cohorts;

public interface IMentorCohortQueryService
{
    Task<IReadOnlyList<CohortSummary>> GetMyCohortsAsync(
        string mentorUserId);
}