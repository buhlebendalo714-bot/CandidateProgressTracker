using CandidateProgressTracker.Domain.Cohorts;

namespace CandidateProgressTracker.Application.Learning;

public record MemberAssignmentSummary(
    Guid AssignmentId,
    string CohortName,
    string CycleName,
    string ProjectTitle,
    string MilestoneTitle,
    string OwnerName,
    bool IsTeamAssignment,
    bool IsRequired,
    DateTimeOffset DueAt,
    int RevisionNo,
    string Instructions,
    string AcceptanceExpectations,
    string EvidencePolicy);

public record MemberAssignmentPage(
    IReadOnlyList<MemberAssignmentSummary> Assignments,
    int PageNumber,
    bool HasNextPage);

public interface IMemberAssignmentQueryService
{
    Task<MemberAssignmentPage?> GetAsync(
        string userId,
        MembershipKind membershipKind,
        int pageNumber = 1);
}