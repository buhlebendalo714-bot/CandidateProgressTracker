namespace CandidateProgressTracker.Domain.Auditing;
public sealed class AuditEvent
{
    public Guid AuditEventId { get; set; } = Guid.NewGuid();
    public string? ActorId { get; set; }
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
