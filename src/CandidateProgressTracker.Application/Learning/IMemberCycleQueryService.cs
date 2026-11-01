using CandidateProgressTracker.Domain.Cohorts;

namespace CandidateProgressTracker.Application.Learning;

public interface IMemberCycleQueryService
{
    Task<CohortCycles?> GetAsync(
        Guid cohortId,
        string userId,
        MembershipKind membershipKind);
}