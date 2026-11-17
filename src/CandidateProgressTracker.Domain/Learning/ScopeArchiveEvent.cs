namespace CandidateProgressTracker.Domain.Learning;
public sealed class ScopeArchiveEvent
{
    public Guid ScopeArchiveEventId {get;set;}=Guid.NewGuid();
    public Guid ScopeId {get;set;}
    public string ScopeType {get;set;}="";
    public string ActorId {get;set;}="";
    public Guid RequestId {get;set;}
    public string ExpectedVersion {get;set;}="";
    public bool Archived {get;set;}
    public string Reason {get;set;}="";
    public DateTimeOffset OccurredAt {get;set;}
    public string OutcomeSnapshot {get;set;}="";
}
