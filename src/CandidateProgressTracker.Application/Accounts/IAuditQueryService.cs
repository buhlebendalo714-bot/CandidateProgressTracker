namespace CandidateProgressTracker.Application.Accounts;
public sealed record AuditFilter(string? Actor=null,string? Action=null,string? Record=null,DateTimeOffset? From=null,DateTimeOffset? To=null,int Page=1);
public sealed record AuditRow(Guid Id,string? ActorId,string EntityType,string EntityId,string Action,string Details,DateTimeOffset OccurredAt,string Source)
{
    public string? ActorName { get; init; }
    public string? AffectedName { get; init; }
    public string? DisplayAction { get; init; }
    public string? Reason { get; init; }
}
public sealed record AuditPage(int Page,bool HasNext,IReadOnlyList<AuditRow> Rows);
public interface IAuditQueryService { Task<AuditPage?> SearchAsync(AuditFilter filter,string actor); }
