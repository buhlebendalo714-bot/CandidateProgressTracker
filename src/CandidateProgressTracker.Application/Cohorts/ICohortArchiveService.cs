namespace CandidateProgressTracker.Application.Cohorts;

public record CohortArchiveResult(
    bool Succeeded,
    IReadOnlyList<string> Errors);

public interface ICohortArchiveService
{
    Task<CohortArchiveResult> ArchiveAsync(
        Guid cohortId,
        string reason,
        byte[] expectedRowVersion,
        string actorUserId);
}