using CandidateProgressTracker.Domain.Cohorts;

namespace CandidateProgressTracker.Application.Cohorts;

public record MembershipResult(
    bool Succeeded,
    Guid? MembershipId,
    IReadOnlyList<string> Errors);

public interface ICohortMembershipService
{
    Task<MembershipResult> AssignAsync(
        Guid cohortId,
        string memberUserId,
        MembershipKind membershipKind,
        string changeReason,
        string actorUserId);
}