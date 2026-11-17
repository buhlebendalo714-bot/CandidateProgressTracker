namespace CandidateProgressTracker.Application.Learning;
public sealed record ArchiveScope(Guid Id,string Kind,string Name,bool Archived,string Version);
public sealed record ArchiveAssignment(Guid AssignmentId,string Title,string Owner);
public sealed record ArchiveOutcome(Guid AssignmentId,string Title,string Owner,bool IsCourse,bool IsRequired,string Status,Guid? ReceiptId,int? RevisionNo,bool Approved);
public sealed record ArchiveAction(DateTimeOffset OccurredAt,string Actor,string Action,string Reason,IReadOnlyList<ArchiveOutcome> Outcomes);
public sealed record ArchiveDetails(ArchiveScope Scope,IReadOnlyList<ArchiveAssignment> Assignments,IReadOnlyList<ArchiveAction> Events);
public sealed record ArchiveRequest(Guid ScopeId,string Kind,string Version,bool Archive,string Reason,Guid RequestId);
public interface IArchivedHistoryService
{
    Task<IReadOnlyList<ArchiveScope>?> GetScopesAsync(string actor);
    Task<ArchiveDetails?> GetAsync(Guid scopeId,string kind,string actor);
    Task<string?> ChangeAsync(ArchiveRequest request,string actor);
    Task<SubmissionHistoryPage?> HistoryAsync(Guid assignmentId,string actor,int page=1);
}
