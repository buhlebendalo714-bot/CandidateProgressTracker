namespace CandidateProgressTracker.Domain.Learning;

public class AssignmentRevision
{
    public Guid RevisionId { get; set; } = Guid.NewGuid();

    public Guid AssignmentId { get; set; }

    public int RevisionNo { get; set; }

    public DateTimeOffset DueAt { get; set; }

    public string InstructionsSnapshot { get; set; } = string.Empty;

    public string AcceptanceSnapshot { get; set; } = string.Empty;

    public string EvidencePolicySnapshot { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }

    public string ChangedBy { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}