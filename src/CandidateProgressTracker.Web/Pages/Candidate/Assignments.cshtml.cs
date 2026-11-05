using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Web.Pages.Shared;
using Microsoft.AspNetCore.Authorization;

namespace CandidateProgressTracker.Web.Pages.Candidate;

[Authorize(Roles = "Candidate")]
public class AssignmentsModel : MemberAssignmentsPageModel
{
    public AssignmentsModel(IMemberAssignmentQueryService service)
        : base(service, MembershipKind.Candidate)
    {
    }
}