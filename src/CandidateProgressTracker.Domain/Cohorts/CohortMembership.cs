namespace CandidateProgressTracker.Domain.Cohorts;

public class CohortMembership
{
    public Guid MembershipId { get; set; } = Guid.NewGuid();

    public Guid CohortId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public MembershipKind MembershipKind { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public string? EndedBy { get; set; }

    public string? EndReason { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string ChangeReason { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}