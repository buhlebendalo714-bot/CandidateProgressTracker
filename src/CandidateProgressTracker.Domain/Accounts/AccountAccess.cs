namespace CandidateProgressTracker.Domain.Accounts;

// Missing state represents an existing, active account. No historical backfill.
public sealed class AccountAccessState
{
    public string UserId { get; set; } = "";
    public bool Enabled { get; set; }
    public long PermissionVersion { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string ChangedBy { get; set; } = "";
    public string Reason { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}

public sealed class AccountCredential
{
    public Guid CredentialId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string IssuedBy { get; set; } = "";
}

public sealed class AccountChange
{
    public Guid AccountChangeId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
