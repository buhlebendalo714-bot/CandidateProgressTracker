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
        bool IsArchived);

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
            await _users.IsInRoleAsync(user, "Administrator");
    }

    private Task<Parent?> GetParentAsync(Guid assignmentId)
    {
        return (
            from assignment in _db.Assignments.AsNoTracking()
            join milestone in _db.Milestones.AsNoTracking()
                on assignment.MilestoneId equals milestone.MilestoneId
            join project in _db.Projects.AsNoTracking()
                on assignment.ProjectId equals project.ProjectId
            join cycle in _db.LearningCycles.AsNoTracking()
                on project.CycleId equals cycle.CycleId
            join cohort in _db.Cohorts.AsNoTracking()
                on cycle.CohortId equals cohort.CohortId
            where assignment.AssignmentId == assignmentId
            select new Parent(
                milestone.MilestoneId,
                milestone.Title,
                project.IsArchived || cycle.IsArchived || cohort.IsArchived))
            .SingleOrDefaultAsync();
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
            Convert.ToBase64String(row.RowVersion));
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