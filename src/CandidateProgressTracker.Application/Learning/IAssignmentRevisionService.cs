namespace CandidateProgressTracker.Application.Learning;

public record AssignmentRevisionDetails(
    Guid AssignmentId,
    Guid MilestoneId,
    string MilestoneTitle,
    bool CanManage,
    int RevisionNo,
    DateTimeOffset DueAt,
    string Instructions,
    string AcceptanceExpectations,
    string EvidencePolicy,
    string RowVersion);

public record AssignmentRevisionResult(
    bool Succeeded,
    IReadOnlyList<string> Errors);

public interface IAssignmentRevisionService
{
    Task<AssignmentRevisionDetails?> GetAsync(
        Guid assignmentId,
        string actorUserId);

    Task<AssignmentRevisionResult> ReviseAsync(
        Guid assignmentId,
        string rowVersion,
        DateTimeOffset dueAt,
        string instructions,
        string acceptanceExpectations,
        string evidencePolicy,
        string reason,
        string actorUserId);
}