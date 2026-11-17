using CandidateProgressTracker.Domain.Cohorts;

namespace CandidateProgressTracker.Application.Cohorts;

public record MembershipSummary(
    Guid MembershipId,
    string UserName,
    MembershipKind Kind,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    byte[] RowVersion,
    string? EndReason,
    string? EndedByUserName,
    DateTimeOffset? EndedAt);

public record EligibleMember(
    string UserId,
    string UserName,
    MembershipKind Kind);

public record CohortMembershipDetails(
    Guid CohortId,
    string Name,
    string Code,
    bool IsArchived,
    IReadOnlyList<MembershipSummary> Memberships,
    IReadOnlyList<EligibleMember> EligibleMembers);

public interface ICohortMembershipQueryService
{
    Task<CohortMembershipDetails?> GetAsync(Guid cohortId, string actorUserId);
}
