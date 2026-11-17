using CandidateProgressTracker.Domain.Learning;

namespace CandidateProgressTracker.Application.Learning;

public static class ReviewLimits
{
    public const int Feedback = 10000;
    public const int ReopeningReason = 1000;
}

public record ReviewRequest(Guid AssignmentId, Guid SubmissionId, string RowVersion,
    Guid RequestId, ReviewDecision Decision, string Feedback);
public record ReopeningRequest(Guid AssignmentId, Guid ApprovalReviewId, string RowVersion,
    Guid RequestId, string Reason);
public record WorkflowResult(bool Succeeded, Guid? ReceiptId, bool Conflict, IReadOnlyList<string> Errors);
public record ReopeningHistory(Guid ReopeningId, string AdministratorName, DateTimeOffset ReopenedAt, string Reason);
public record ReviewHistory(Guid ReviewId, int ReviewNo, ReviewDecision Decision, string Feedback,
    string ReviewerName, DateTimeOffset DecidedAt, string Status, ReopeningHistory? Reopening);
public record ReviewPage(Guid AssignmentId, string CandidateName, string RowVersion,
    SubmissionHistoryAttempt? Attempt, bool CanDecide, string Eligibility, Guid? EffectiveApprovalId)
{
    public CreditedRoster? Roster { get; init; }
}

public interface IIndividualSubmissionReviewService
{
    Task<ReviewPage?> GetReviewAsync(Guid assignmentId, string mentorId);
    Task<WorkflowResult> DecideAsync(ReviewRequest request, string mentorId);
}
public interface IAssignmentReopeningService
{
    Task<ReviewPage?> GetReopeningAsync(Guid assignmentId, string administratorId);
    Task<WorkflowResult> ReopenAsync(ReopeningRequest request, string administratorId);
}
