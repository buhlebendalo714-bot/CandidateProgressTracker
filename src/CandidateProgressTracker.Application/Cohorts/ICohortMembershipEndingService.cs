namespace CandidateProgressTracker.Application.Cohorts;

public interface ICohortMembershipEndingService
{
    Task<MembershipResult> EndAsync(
        Guid membershipId,
        string endReason,
        byte[] expectedRowVersion,
        string actorUserId);
}