using System.Data;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public class CohortMembershipEndingService
    : ICohortMembershipEndingService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public CohortMembershipEndingService(
        TrackerDbContext db,
        UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<MembershipResult> EndAsync(
        Guid membershipId,
        string endReason,
        byte[] expectedRowVersion,
        string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        if (membershipId == Guid.Empty)
        {
            return Failure("Select a membership.");
        }

        var reason = endReason?.Trim() ?? string.Empty;

        if (reason.Length == 0 || reason.Length > 1000)
        {
            return Failure(
                "Enter an ending reason of up to 1000 characters.");
        }

        if (expectedRowVersion is null || expectedRowVersion.Length != 8)
        {
            return Failure(
                "The membership version is missing or invalid. Reload the page.");
        }

        await using var transaction = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var actor = await _userManager.FindByIdAsync(actorUserId);

        if (actor is null ||
            !await _userManager.IsInRoleAsync(actor, "Administrator"))
        {
            return Failure("Administrator access is required.");
        }

        var membership = await _db.CohortMemberships
            .SingleOrDefaultAsync(item =>
                item.MembershipId == membershipId);

        if (membership is null)
        {
            return Failure("The selected membership was not found.");
        }

        if (!membership.RowVersion.SequenceEqual(expectedRowVersion))
        {
            return Failure(
                "This membership has changed. Reload the page before trying again.");
        }

        var cohort = await _db.Cohorts
            .SingleAsync(item => item.CohortId == membership.CohortId);

        if (cohort.IsArchived)
        {
            return Failure(
                "Memberships cannot be changed in an archived cohort.");
        }

        if (membership.EndsAt is not null)
        {
            return Failure("This membership already has an end date.");
        }

        var now = DateTimeOffset.UtcNow;

        if (now <= membership.StartsAt)
        {
            return Failure(
                "The membership must have started before it can be ended.");
        }

        membership.EndsAt = now;
        membership.EndedAt = now;
        membership.EndedBy = actor.Id;
        membership.EndReason = reason;

        _db.Entry(membership)
            .Property(item => item.RowVersion)
            .OriginalValue = expectedRowVersion;

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            _db.Entry(membership).State = EntityState.Detached;

            return Failure(
                "This membership has changed. Reload the page before trying again.");
        }

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