using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class MilestoneAssignmentService : IMilestoneAssignmentService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public MilestoneAssignmentService(
        TrackerDbContext db,
        UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    private record Target(
        Guid MilestoneId,
        Guid ProjectId,
        Guid CycleId,
        Guid CohortId,
        string ProjectTitle,
        string MilestoneTitle,
        ProjectMode Mode,
        bool IsArchived,
        string Instructions,
        string AcceptanceExpectations,
        string EvidencePolicy);

    private static MilestoneAssignmentResult Failure(string message) =>
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

    private Task<Target?> GetTargetAsync(Guid milestoneId)
    {
        return (
            from milestone in _db.Milestones.AsNoTracking()
            join project in _db.Projects.AsNoTracking()
                on milestone.ProjectId equals project.ProjectId
            join cycle in _db.LearningCycles.AsNoTracking()
                on project.CycleId equals cycle.CycleId
            join cohort in _db.Cohorts.AsNoTracking()
                on cycle.CohortId equals cohort.CohortId
            where milestone.MilestoneId == milestoneId
            select new Target(
                milestone.MilestoneId,
                project.ProjectId,
                cycle.CycleId,
                cohort.CohortId,
                project.Title,
                milestone.Title,
                project.Mode,
                project.IsArchived || cycle.IsArchived || cohort.IsArchived,
                milestone.Instructions,
                milestone.AcceptanceExpectations,
                milestone.EvidencePolicy))
            .SingleOrDefaultAsync();
    }

    public async Task<MilestoneAssignmentDetails?> GetAsync(
        Guid milestoneId,
        string actorUserId)
    {
        if (!await IsAdministratorAsync(actorUserId))
        {
            return null;
        }

        var target = await GetTargetAsync(milestoneId);

        if (target is null)
        {
            return null;
        }

        var canManage = !target.IsArchived &&
            (target.Mode == ProjectMode.Individual ||
             target.Mode == ProjectMode.Team);

        var candidates = new List<AssignmentCandidateOption>();
        var teams = new List<AssignmentTeamOption>();

        if (canManage && target.Mode == ProjectMode.Individual)
        {
            var now = DateTimeOffset.UtcNow;

            var candidateRoleHolders =
                from userRole in _db.UserRoles
                join role in _db.Roles
                    on userRole.RoleId equals role.Id
                where role.NormalizedName == "CANDIDATE"
                select userRole.UserId;

            candidates = await _db.Users
                .AsNoTracking()
                .Where(user =>
                    candidateRoleHolders.Contains(user.Id) &&
                    _db.CohortMemberships.Any(membership =>
                        membership.CohortId == target.CohortId &&
                        membership.UserId == user.Id &&
                        membership.MembershipKind == MembershipKind.Candidate &&
                        membership.StartsAt <= now &&
                        (membership.EndsAt == null || membership.EndsAt > now)) &&
                    !_db.Assignments.Any(assignment =>
                        assignment.MilestoneId == milestoneId &&
                        assignment.CandidateId == user.Id))
                .OrderBy(user => user.UserName)
                .ThenBy(user => user.Id)
                .Select(user => new AssignmentCandidateOption(
                    user.Id,
                    user.UserName ?? "(No username)"))
                .ToListAsync();
        }

        if (canManage && target.Mode == ProjectMode.Team)
        {
            teams = await _db.ProjectTeams
                .AsNoTracking()
                .Where(team =>
                    team.ProjectId == target.ProjectId &&
                    !_db.Assignments.Any(assignment =>
                        assignment.MilestoneId == milestoneId &&
                        assignment.TeamId == team.TeamId))
                .OrderBy(team => team.Name)
                .ThenBy(team => team.TeamId)
                .Select(team => new AssignmentTeamOption(
                    team.TeamId, team.Name))
                .ToListAsync();
        }

        var assignments = await _db.Assignments
            .AsNoTracking()
            .Where(assignment => assignment.MilestoneId == milestoneId)
            .OrderBy(assignment => assignment.DueAt)
            .ThenBy(assignment => assignment.AssignmentId)
            .Select(assignment => new AssignmentSummary(
                assignment.AssignmentId,
                assignment.CandidateId != null
                    ? (_db.Users
                        .Where(user => user.Id == assignment.CandidateId)
                        .Select(user => user.UserName)
                        .FirstOrDefault() ?? "(No username)")
                    : (_db.ProjectTeams
                        .Where(team => team.TeamId == assignment.TeamId)
                        .Select(team => team.Name)
                        .FirstOrDefault() ?? "(Unknown team)"),
                assignment.DueAt,
                assignment.IsRequired))
            .ToListAsync();

        return new MilestoneAssignmentDetails(
            target.MilestoneId,
            target.ProjectId,
            target.ProjectTitle,
            target.MilestoneTitle,
            target.Mode == ProjectMode.Team,
            canManage,
            candidates,
            teams,
            assignments);
    }

    public async Task<MilestoneAssignmentResult> CreateAsync(
        Guid milestoneId,
        string? candidateId,
        Guid? teamId,
        DateTimeOffset dueAt,
        bool isRequired,
        string actorUserId)
    {
        var ownerId = string.IsNullOrWhiteSpace(candidateId)
            ? null
            : candidateId.Trim();

        var hasCandidate = ownerId is not null;
        var hasTeam = teamId.HasValue;

        if (hasCandidate == hasTeam)
        {
            return Failure("Select exactly one candidate or team.");
        }

        if (teamId == Guid.Empty)
        {
            return Failure("Select a valid team.");
        }

        var dueUtc = dueAt.ToUniversalTime();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var target = await GetTargetAsync(milestoneId);

        if (target is null)
        {
            return Failure("The selected milestone was not found.");
        }

        if (target.IsArchived)
        {
            return Failure(
                "Assignments require an unarchived project, cycle, and cohort.");
        }

        var now = DateTimeOffset.UtcNow;

        if (dueUtc <= now)
        {
            return Failure("Choose a future deadline.");
        }

        if (target.Mode == ProjectMode.Individual)
        {
            if (!hasCandidate || hasTeam)
            {
                return Failure("Individual projects require a candidate owner.");
            }

            var candidate = await _users.FindByIdAsync(ownerId!);

            if (candidate is null ||
                !await _users.IsInRoleAsync(candidate, "Candidate"))
            {
                return Failure("The selected user must have the Candidate role.");
            }

            var eligible = await _db.CohortMemberships.AnyAsync(
                membership =>
                    membership.CohortId == target.CohortId &&
                    membership.UserId == ownerId &&
                    membership.MembershipKind == MembershipKind.Candidate &&
                    membership.StartsAt <= now &&
                    (membership.EndsAt == null || membership.EndsAt > now));

            if (!eligible)
            {
                return Failure(
                    "The candidate must currently belong to this cohort.");
            }
        }
        else if (target.Mode == ProjectMode.Team)
        {
            if (!hasTeam || hasCandidate)
            {
                return Failure("Team projects require a team owner.");
            }

            if (!await _db.ProjectTeams.AnyAsync(team =>
                team.TeamId == teamId &&
                team.ProjectId == target.ProjectId))
            {
                return Failure("Select a team belonging to this project.");
            }
        }
        else
        {
            return Failure("The project has an unsupported ownership mode.");
        }

        var duplicate = await _db.Assignments.AnyAsync(assignment =>
            assignment.MilestoneId == milestoneId &&
            ((ownerId != null && assignment.CandidateId == ownerId) ||
             (teamId != null && assignment.TeamId == teamId)));

        if (duplicate)
        {
            return Failure("This owner already has an assignment for this milestone.");
        }

        var assignment = new Assignment
        {
            CycleId = target.CycleId,
            ProjectId = target.ProjectId,
            MilestoneId = target.MilestoneId,
            CandidateId = ownerId,
            TeamId = teamId,
            IsRequired = isRequired,
            DueAt = dueUtc,
            CreatedAt = now,
            CreatedBy = actorUserId
        };

        var revision = new AssignmentRevision
        {
            AssignmentId = assignment.AssignmentId,
            RevisionNo = 1,
            DueAt = dueUtc,
            InstructionsSnapshot = target.Instructions,
            AcceptanceSnapshot = target.AcceptanceExpectations,
            EvidencePolicySnapshot = target.EvidencePolicy,
            ChangedAt = now,
            ChangedBy = actorUserId,
            Reason = "Initial assignment"
        };

        _db.Assignments.Add(assignment);
        _db.AssignmentRevisions.Add(revision);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new MilestoneAssignmentResult(
                true, Array.Empty<string>());
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627) &&
                  (sql.Message.Contains("IX_Assignments_MilestoneId_CandidateId") ||
                   sql.Message.Contains("IX_Assignments_MilestoneId_TeamId")))
        {
            await transaction.RollbackAsync();

            _db.Entry(revision).State = EntityState.Detached;
            _db.Entry(assignment).State = EntityState.Detached;

            return Failure(
                "This owner already has an assignment for this milestone.");
        }
    }
}