namespace CandidateProgressTracker.Domain.Cohorts;
public sealed class OnboardingBatch
{
    public Guid BatchId { get; set; }
    public Guid CohortId { get; set; }
    public string ActorId { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public string Fingerprint { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class OnboardingBatchRow
{
    public Guid BatchId { get; set; }
    public int Row { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Action { get; set; } = "";
    public string? ExpectedUserId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Message { get; set; }
    public string? UserId { get; set; }
    public Guid? MembershipId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
