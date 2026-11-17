using CandidateProgressTracker.Infrastructure.Learning;
using System.Data;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public class CohortMembershipService : ICohortMembershipService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public CohortMembershipService(
        TrackerDbContext db,
        UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<MembershipResult> AssignAsync(
        Guid cohortId,
        string memberUserId,
        MembershipKind membershipKind,
        string changeReason,
        string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        if (cohortId == Guid.Empty)
        {
            return Failure("Select a cohort.");
        }

        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            return Failure("Select a user.");
        }

        var requiredRole = membershipKind switch
        {
            MembershipKind.Candidate => "Candidate",
            MembershipKind.Mentor => "Mentor",
            _ => null
        };

        if (requiredRole is null)
        {
            return Failure("Select Candidate or Mentor.");
        }

        var reason = changeReason?.Trim() ?? string.Empty;

        if (reason.Length == 0 || reason.Length > 1000)
        {
            return Failure(
                "Enter a membership reason of up to 1000 characters.");
        }

        // Keep eligibility checks and the insert in one transaction.
        await using var transaction = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        await CohortWorkflowGate.LockAsync(_db, cohortId);

        var actor = await _userManager.FindByIdAsync(actorUserId);

        if (actor is null ||
            !(await CandidateProgressTracker.Infrastructure.Identity.AccountAvailability.IsActiveAsync(_db, actor.Id) && await _userManager.IsInRoleAsync(actor, "Administrator")))
        {
            return Failure("Administrator access is required.");
        }

        var cohort = await _db.Cohorts
            .SingleOrDefaultAsync(item => item.CohortId == cohortId);

        if (cohort is null)
        {
            return Failure("The selected cohort was not found.");
        }

        if (cohort.IsArchived)
        {
            return Failure("Members cannot be added to an archived cohort.");
        }

        var member = await _userManager.FindByIdAsync(memberUserId);

        if (member is null)
        {
            return Failure("The selected user was not found.");
        }

        if (!(await CandidateProgressTracker.Infrastructure.Identity.AccountAvailability.IsActiveAsync(_db, member.Id) && await _userManager.IsInRoleAsync(member, requiredRole)))
        {
            return Failure(
                $"The selected user must have the {requiredRole} role.");
        }

        var startsAt = DateTimeOffset.UtcNow;

        // An immediate, open-ended membership must not overlap an
        // existing interval of the same kind, including future intervals.
        var overlaps = await _db.CohortMemberships.AnyAsync(item =>
            item.CohortId == cohortId &&
            item.UserId == memberUserId &&
            item.MembershipKind == membershipKind &&
            (item.EndsAt == null || item.EndsAt > startsAt));

        if (overlaps)
        {
            return Failure(
                "This user already has an overlapping membership " +
                "of this kind in the cohort.");
        }

        var membership = new CohortMembership
        {
            CohortId = cohortId,
            UserId = member.Id,
            MembershipKind = membershipKind,
            StartsAt = startsAt,
            EndsAt = null,
            ChangeReason = reason,
            CreatedBy = actor.Id
        };

        _db.CohortMemberships.Add(membership);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new MembershipResult(
            true,
            membership.MembershipId,
            Array.Empty<string>());
    }

    private static MembershipResult Failure(string message)
    {
        return new MembershipResult(
            false,
            null,
            new[] { message });
    }
}