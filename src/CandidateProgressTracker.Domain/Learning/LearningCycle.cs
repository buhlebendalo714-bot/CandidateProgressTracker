namespace CandidateProgressTracker.Domain.Learning;

public class LearningCycle
{
    public Guid CycleId { get; set; } = Guid.NewGuid();

    public Guid CohortId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartsOn { get; set; }

    public DateOnly EndsOn { get; set; }

    public bool IsArchived { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public string? ArchivedBy { get; set; }

    public string? ArchiveReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}