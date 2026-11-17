namespace CandidateProgressTracker.Domain.Learning;

public enum ReviewDecision : byte { Approved = 1, ChangesRequested = 2 }

public class SubmissionReview
{
    public Guid ReviewId { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid SubmissionId { get; set; }
    public int ReviewNo { get; set; }
    public ReviewDecision Decision { get; set; }
    public string Feedback { get; set; } = "";
    public string ReviewerId { get; set; } = "";
    public DateTimeOffset DecidedAt { get; set; }
    public Guid RequestId { get; set; }
}

public class AssignmentReopening
{
    public Guid ReopeningId { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid ApprovalReviewId { get; set; }
    public string AdministratorId { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset ReopenedAt { get; set; }
    public Guid RequestId { get; set; }
}
