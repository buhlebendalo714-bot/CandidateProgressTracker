namespace CandidateProgressTracker.Domain.Learning;

public class SubmissionEvidence
{
    public Guid SubmissionEvidenceId { get; set; } = Guid.NewGuid();

    public Guid SubmissionId { get; set; }

    public int SequenceNo { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}