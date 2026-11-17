namespace CandidateProgressTracker.Domain.Learning;

public sealed class AssignmentCreditedRoster
{
    public Guid AssignmentId { get; set; }
    public Guid TeamId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CohortId { get; set; }
    public string TeamNameSnapshot { get; set; } = "";
    public DateTimeOffset FrozenAt { get; set; }
    public string FrozenBy { get; set; } = "";
}

public sealed class AssignmentCreditedRosterMember
{
    public Guid AssignmentId { get; set; }
    public Guid TeamId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CohortId { get; set; }
    public string UserId { get; set; } = "";
    public string UserNameSnapshot { get; set; } = "";
    public Guid TeamMembershipId { get; set; }
    public Guid CohortMembershipId { get; set; }
}
