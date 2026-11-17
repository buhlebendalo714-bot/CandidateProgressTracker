namespace CandidateProgressTracker.Application.Learning;

public record CreditedMember(string UserId, string UserName, Guid TeamMembershipId, Guid CohortMembershipId);
public record CreditedRoster(string TeamName, DateTimeOffset FrozenAt, string FrozenBy,
    IReadOnlyList<CreditedMember> Members);
public record TeamSubmissionRequest(IndividualSubmissionRequest Content, string RosterConfirmation);
public record TeamSubmissionForm(IndividualSubmissionDetails Requirements, string TeamName,
    IReadOnlyList<CreditedMember> Preview, CreditedRoster? Roster, string Confirmation,
    bool CanSubmit, string Eligibility);
public interface ITeamSubmissionService
{
    Task<TeamSubmissionForm?> GetFormAsync(Guid assignmentId, string userId);
    Task<SubmissionResult> SubmitAsync(TeamSubmissionRequest request, string userId);
    Task<SubmissionHistoryPage?> GetHistoryAsync(Guid assignmentId, string userId, int pageNumber = 1);
    Task<SubmissionHistoryPage?> GetMentorHistoryAsync(Guid assignmentId, string mentorId, int pageNumber = 1);
    Task<IReadOnlyList<TeamHistoryLink>> GetCreditedHistoryAsync(string userId);
}
public record TeamHistoryLink(Guid AssignmentId, string TeamName, string MilestoneTitle);
