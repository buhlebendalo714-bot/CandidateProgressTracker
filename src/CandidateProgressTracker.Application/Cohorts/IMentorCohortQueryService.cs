namespace CandidateProgressTracker.Application.Cohorts;

public record MentorCandidateSummary(
    string UserId,
    string UserName);

public record MentorCohortDetails(
    Guid CohortId,
    string Name,
    string Code,
    string? LocationLabel,
    IReadOnlyList<MentorCandidateSummary> Candidates);

public interface IMentorCohortQueryService
{
    Task<IReadOnlyList<CohortSummary>> GetMyCohortsAsync(
        string mentorUserId);

    Task<MentorCohortDetails?> GetDetailsAsync(
        Guid cohortId,
        string mentorUserId);
}