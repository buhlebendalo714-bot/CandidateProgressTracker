namespace CandidateProgressTracker.Domain.Learning;

public class TeamMembership
{
    public Guid TeamMembershipId { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }

    public Guid ProjectId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public string ChangeReason { get; set; } = string.Empty;

    public string ChangedBy { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public string? EndedBy { get; set; }

    public string? EndReason { get; set; }
}