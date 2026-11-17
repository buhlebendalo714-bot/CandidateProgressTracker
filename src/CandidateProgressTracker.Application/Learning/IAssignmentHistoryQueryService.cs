namespace CandidateProgressTracker.Application.Learning;

public record AssignmentHistoryEntry(
    int RevisionNo,
    DateTimeOffset DueAt,
    string Instructions,
    string AcceptanceExpectations,
    string EvidencePolicy,
    DateTimeOffset ChangedAt,
    string ChangedBy,
    string ChangedByName,
    string Reason);

public record AssignmentHistoryPage(
    Guid AssignmentId,
    Guid MilestoneId,
    string MilestoneTitle,
    string OwnerName,
    bool IsArchived,
    IReadOnlyList<AssignmentHistoryEntry> Revisions,
    int PageNumber,
    bool HasNextPage)
{
    public Guid CycleId { get; init; }
}

public interface IAssignmentHistoryQueryService
{
    Task<AssignmentHistoryPage?> GetAsync(
        Guid assignmentId,
        string actorUserId,
        int pageNumber = 1);
}