namespace CandidateProgressTracker.Application.Learning;
public record CycleChoice(Guid CycleId,string Name,Guid CohortId,string CohortName);
public record ProgressAssignment(Guid AssignmentId,string Title,string Group,bool IsCourse,bool IsTeam,bool IsRequired,bool CountsForProgress,
    string Status,bool NeedsCandidateAction,bool CanReview,DateTimeOffset DueAt,DateTimeOffset? ReceivedAt,string Owner,bool CanSubmit);
public record ProgressTotals(int Approved,int Required,int Pending,int ChangesRequested,int Overdue)
{
    public decimal? Percentage=>Required==0 ? null : Math.Round(Approved*100m/Required,1,MidpointRounding.AwayFromZero);
}
public record CandidateFeedback(Guid AssignmentId,string Title,bool IsTeam,int AttemptNo,string Decision,string Feedback,string Mentor,DateTimeOffset DecidedAt);
public record CandidateOverview(Guid? SelectedCycle,IReadOnlyList<CycleChoice> Cycles,ProgressTotals Theory,ProgressTotals Projects,IReadOnlyList<ProgressAssignment> Assignments)
{
    public IReadOnlyList<CandidateFeedback> RecentFeedback { get; init; } = [];
}
public record CandidateProgressRow(string CandidateId,string Name,ProgressTotals Theory,ProgressTotals Projects);
public record MentorOverview(Guid? SelectedCycle,IReadOnlyList<CycleChoice> Cycles,IReadOnlyList<CandidateProgressRow> Candidates);
public record ReviewQueueFilter(Guid? CohortId=null,Guid? CycleId=null,Guid? ProjectId=null,Guid? OfferingId=null,string Status="Awaiting review",int Page=1);
public record QueueTargetChoice(Guid Id,string Name);
public record ReviewQueuePage(ReviewQueueFilter Filter,IReadOnlyList<CycleChoice> Cycles,IReadOnlyList<ProgressAssignment> Rows,bool HasNextPage)
{
    public IReadOnlyList<QueueTargetChoice> Projects {get;init;}=[];
    public IReadOnlyList<QueueTargetChoice> Offerings {get;init;}=[];
}
public interface ILearningOverviewService
{
    Task<CandidateOverview?> GetCandidateAsync(string actor,Guid? cycleId=null);
    Task<MentorOverview?> GetMentorAsync(string actor,Guid? cycleId=null);
    Task<ReviewQueuePage?> GetQueueAsync(string actor,ReviewQueueFilter filter);
}
