namespace CandidateProgressTracker.Domain.Learning;

public class Milestone
{
    public Guid MilestoneId { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public int SequenceNo { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string AcceptanceExpectations { get; set; } = string.Empty;

    public string EvidencePolicy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}