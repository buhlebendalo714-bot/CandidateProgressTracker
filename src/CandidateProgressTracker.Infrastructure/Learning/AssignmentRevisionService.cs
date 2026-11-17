using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class AssignmentRevisionService : IAssignmentRevisionService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public AssignmentRevisionService(
        TrackerDbContext db,
        UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    private record Parent(
        Guid MilestoneId,
        string Title,
        bool IsArchived, Guid CycleId);

    private static AssignmentRevisionResult Failure(string message) =>
        new(false, new[] { message });

    private async Task<bool> IsAdministratorAsync(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return false;
        }

        var user = await _users.FindByIdAsync(actorId);

        return user is not null &&
            (await CandidateProgressTracker.Infrastructure.Identity.AccountAvailability.IsActiveAsync(_db, user.Id) && await _users.IsInRoleAsync(user, "Administrator"));
    }

    private Task<Parent?> GetParentAsync(Guid assignmentId)
    {
        return GetParentCoreAsync(assignmentId);
    }
    private async Task<Parent?> GetParentCoreAsync(Guid id)
    {
        var a = await _db.Assignments.AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == id);
        if (a is null) return null;
        var c = await _db.LearningCycles.AsNoTracking().SingleAsync(c => c.CycleId == a.CycleId);
        var h = await _db.Cohorts.AsNoTracking().SingleAsync(h => h.CohortId == c.CohortId);
        if (a.OfferingId is not null)
        {
            var o = await _db.CourseOfferings.AsNoTracking().SingleAsync(o => o.OfferingId == a.OfferingId);
            return new(Guid.Empty, o.TitleSnapshot, c.IsArchived || h.IsArchived, c.CycleId);
        }
        var p = await _db.Projects.AsNoTracking().SingleAsync(p => p.ProjectId == a.ProjectId);
        var m = await _db.Milestones.AsNoTracking().SingleAsync(m => m.MilestoneId == a.MilestoneId);
        return new(m.MilestoneId, m.Title, p.IsArchived || c.IsArchived || h.IsArchived, c.CycleId);
    }

    public async Task<AssignmentRevisionDetails?> GetAsync(
        Guid assignmentId,
        string actorUserId)
    {
        if (!await IsAdministratorAsync(actorUserId))
        {
            return null;
        }

        var parent = await GetParentAsync(assignmentId);

        if (parent is null)
        {
            return null;
        }

        var row = await (
            from assignment in _db.Assignments.AsNoTracking()
            join revision in _db.AssignmentRevisions.AsNoTracking()
                on assignment.AssignmentId equals revision.AssignmentId
            where assignment.AssignmentId == assignmentId
            orderby revision.RevisionNo descending
            select new
            {
                assignment.RowVersion,
                revision.RevisionNo,
                revision.DueAt,
                revision.InstructionsSnapshot,
                revision.AcceptanceSnapshot,
                revision.EvidencePolicySnapshot
            })
            .FirstOrDefaultAsync();

        if (row is null)
        {
            return null;
        }

        return new AssignmentRevisionDetails(
            assignmentId,
            parent.MilestoneId,
            parent.Title,
            !parent.IsArchived,
            row.RevisionNo,
            row.DueAt,
            row.InstructionsSnapshot,
            row.AcceptanceSnapshot,
            row.EvidencePolicySnapshot,
            Convert.ToBase64String(row.RowVersion)) { CycleId = parent.CycleId };
    }

    public async Task<AssignmentRevisionResult> ReviseAsync(
        Guid assignmentId,
        string rowVersion,
        DateTimeOffset dueAt,
        string instructions,
        string acceptanceExpectations,
        string evidencePolicy,
        string reason,
        string actorUserId)
    {
        var cleanInstructions = instructions?.Trim() ?? "";
        var cleanAcceptance = acceptanceExpectations?.Trim() ?? "";
        var cleanEvidence = evidencePolicy?.Trim() ?? "";
        var cleanReason = reason?.Trim() ?? "";

        if (cleanInstructions.Length is < 1 or > 10000)
        {
            return Failure("Enter instructions of up to 10000 characters.");
        }

        if (cleanAcceptance.Length is < 1 or > 10000)
        {
            return Failure(
                "Enter acceptance expectations of up to 10000 characters.");
        }

        if (cleanEvidence.Length is < 1 or > 2000)
        {
            return Failure(
                "Enter evidence requirements of up to 2000 characters.");
        }

        if (cleanReason.Length is < 1 or > 1000)
        {
            return Failure("Enter a revision reason of up to 1000 characters.");
        }

        byte[] expectedVersion;

        try
        {
            expectedVersion = Convert.FromBase64String(rowVersion ?? "");
        }
        catch (FormatException)
        {
            return Failure("Invalid version. Reload the assignment.");
        }

        if (expectedVersion.Length != 8)
        {
            return Failure("Invalid version. Reload the assignment.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await AssignmentWorkflow.LockAsync(_db, assignmentId);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var parent = await GetParentAsync(assignmentId);

        if (parent is null)
        {
            return Failure("The assignment was not found.");
        }

        if (parent.IsArchived)
        {
            return Failure(
                "Archived projects, cycles, and cohorts cannot be revised.");
        }

        var assignment = await _db.Assignments.SingleAsync(
            item => item.AssignmentId == assignmentId);
        await _db.Entry(assignment).ReloadAsync();

        if (!assignment.RowVersion.SequenceEqual(expectedVersion))
        {
            return Failure(
                "This assignment has changed. Reload it before editing again.");
        }

        var current = await _db.AssignmentRevisions
            .AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId)
            .OrderByDescending(item => item.RevisionNo)
            .FirstOrDefaultAsync();

        if (current is null)
        {
            return Failure("The assignment has no saved requirements revision.");
        }

        var dueUtc = dueAt.ToUniversalTime();

        // Requirements may change while retaining an existing past deadline.
        // A newly changed deadline must be in the future.
        if (dueUtc != current.DueAt && dueUtc <= DateTimeOffset.UtcNow)
        {
            return Failure("A changed deadline must be in the future.");
        }

        if (dueUtc == current.DueAt &&
            cleanInstructions == current.InstructionsSnapshot &&
            cleanAcceptance == current.AcceptanceSnapshot &&
            cleanEvidence == current.EvidencePolicySnapshot)
        {
            return Failure("Change the deadline or requirements before saving.");
        }

        if (current.RevisionNo == int.MaxValue)
        {
            return Failure("The revision number limit has been reached.");
        }

        var revision = new AssignmentRevision
        {
            AssignmentId = assignmentId,
            RevisionNo = current.RevisionNo + 1,
            DueAt = dueUtc,
            InstructionsSnapshot = cleanInstructions,
            AcceptanceSnapshot = cleanAcceptance,
            EvidencePolicySnapshot = cleanEvidence,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = actorUserId,
            Reason = cleanReason
        };

        var entry = _db.Entry(assignment);

        entry.Property(item => item.RowVersion).OriginalValue = expectedVersion;

        assignment.DueAt = dueUtc;

        // Force an assignment UPDATE even for requirements-only edits.
        // This advances its rowversion and protects stale forms.
        entry.Property(item => item.DueAt).IsModified = true;

        _db.AssignmentRevisions.Add(revision);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new AssignmentRevisionResult(
                true, Array.Empty<string>());
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();

            _db.Entry(revision).State = EntityState.Detached;
            entry.State = EntityState.Detached;

            return Failure(
                "This assignment changed while saving. Reload it and try again.");
        }
    }
}
