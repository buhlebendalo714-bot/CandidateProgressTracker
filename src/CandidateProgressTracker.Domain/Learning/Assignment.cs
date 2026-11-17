namespace CandidateProgressTracker.Domain.Learning;

public class Assignment
{
    public Guid AssignmentId { get; set; } = Guid.NewGuid();

    public Guid CycleId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? MilestoneId { get; set; }

    public Guid? OfferingId { get; set; }

    // Exactly one owner must be populated.
    public string? CandidateId { get; set; }

    public Guid? TeamId { get; set; }

    public bool IsRequired { get; set; } = true;

    public DateTimeOffset DueAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}