namespace CandidateProgressTracker.Domain.Learning;

public class Submission
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();

    public Guid AssignmentId { get; set; }
    public Guid? CreditedRosterAssignmentId { get; set; }

    public Guid AssignmentRevisionId { get; set; }

    public int AttemptNo { get; set; }

    // Reused when retrying the same submission request.
    public Guid RequestId { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public string SubmittedBy { get; set; } = string.Empty;

    // Server timestamp captured when the submission request is received.
    public DateTimeOffset ReceivedAt { get; set; }

    // Server timestamp captured when preparing the accepted submission.
    // This is not an exact database transaction commit timestamp.
    public DateTimeOffset RecordedAt { get; set; }
}
