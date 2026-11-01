namespace CandidateProgressTracker.Domain.Learning;

public class Project
{
    public Guid ProjectId { get; set; } = Guid.NewGuid();

    public Guid CycleId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Brief { get; set; } = string.Empty;

    public ProjectMode Mode { get; set; }

    public bool IsArchived { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public string? ArchivedBy { get; set; }

    public string? ArchiveReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}