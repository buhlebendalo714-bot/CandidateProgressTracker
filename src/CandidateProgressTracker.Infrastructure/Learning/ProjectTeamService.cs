using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class ProjectTeamService : IProjectTeamService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public ProjectTeamService(
        TrackerDbContext db,
        UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    private record Parent(
        Guid ProjectId,
        Guid CycleId,
        Guid CohortId,
        string Title,
        ProjectMode Mode,
        bool IsArchived);

    private static ProjectTeamResult Failure(string message) =>
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

    private Task<Parent?> GetParentAsync(Guid projectId)
    {
        return (
            from project in _db.Projects.AsNoTracking()
            join cycle in _db.LearningCycles.AsNoTracking()
                on project.CycleId equals cycle.CycleId
            join cohort in _db.Cohorts.AsNoTracking()
                on cycle.CohortId equals cohort.CohortId
            where project.ProjectId == projectId
            select new Parent(
                project.ProjectId,
                cycle.CycleId,
                cohort.CohortId,
                project.Title,
                project.Mode,
                project.IsArchived ||
                cycle.IsArchived ||
                cohort.IsArchived))
            .SingleOrDefaultAsync();
    }

    public async Task<ProjectTeamDetails?> GetAsync(
        Guid projectId,
        string actorUserId)
    {
        if (!await IsAdministratorAsync(actorUserId))
        {
            return null;
        }

        var parent = await GetParentAsync(projectId);

        if (parent is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        var teams = await _db.ProjectTeams
            .AsNoTracking()
            .Where(team => team.ProjectId == projectId)
            .OrderBy(team => team.Name)
            .ThenBy(team => team.TeamId)
            .ToListAsync();

        var members = await (
            from membership in _db.TeamMemberships.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.ProjectId == projectId
                && membership.StartsAt <= now
                && (membership.EndsAt == null ||
                    membership.EndsAt > now)
            orderby user.UserName, user.Id
            select new
            {
                membership.TeamId,
                membership.TeamMembershipId,
                membership.RowVersion,
                user.Id,
                UserName = user.UserName ?? "(No username)"
            })
            .ToListAsync();

        var canManage =
            !parent.IsArchived && parent.Mode == ProjectMode.Team;

        var eligible = new List<TeamMemberSummary>();

        if (canManage)
        {
            var candidateRoleHolders =
                from userRole in _db.UserRoles
                join role in _db.Roles
                    on userRole.RoleId equals role.Id
                where role.NormalizedName == "CANDIDATE" && !_db.AccountAccessStates.Any(s => s.UserId == userRole.UserId && !s.Enabled)
                select userRole.UserId;

            eligible = await _db.Users
                .AsNoTracking()
                .Where(user =>
                    candidateRoleHolders.Contains(user.Id) &&
                    _db.CohortMemberships.Any(membership =>
                        membership.CohortId == parent.CohortId &&
                        membership.UserId == user.Id &&
                        membership.MembershipKind ==
                            MembershipKind.Candidate &&
                        membership.StartsAt <= now &&
                        (membership.EndsAt == null ||
                            membership.EndsAt > now)) &&
                    !_db.TeamMemberships.Any(membership =>
                        membership.ProjectId == projectId &&
                        membership.UserId == user.Id &&
                        (membership.EndsAt == null ||
                            membership.EndsAt > now)))
                .OrderBy(user => user.UserName)
                .ThenBy(user => user.Id)
                .Select(user => new TeamMemberSummary(
                    user.Id,
                    user.UserName ?? "(No username)",
                    Guid.Empty,
                    string.Empty))
                .ToListAsync();
        }

        var summaries = teams
            .Select(team => new ProjectTeamSummary(
                team.TeamId,
                team.Name,
                members
                    .Where(member => member.TeamId == team.TeamId)
                    .Select(member => new TeamMemberSummary(
                        member.Id,
                        member.UserName,
                        member.TeamMembershipId,
                        Convert.ToBase64String(member.RowVersion)))
                    .ToList()))
            .ToList();

        var history = await (
            from membership in _db.TeamMemberships.AsNoTracking()
            join team in _db.ProjectTeams.AsNoTracking()
                on membership.TeamId equals team.TeamId
            join user in _db.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.ProjectId == projectId
            orderby membership.StartsAt descending,
                membership.TeamMembershipId
            select new TeamMembershipHistory(
                team.Name,
                user.UserName ?? "(No username)",
                membership.StartsAt,
                membership.EndsAt,
                membership.ChangeReason,
                membership.ChangedBy,
                membership.EndReason,
                membership.EndedBy))
            .ToListAsync();

        return new ProjectTeamDetails(
            parent.ProjectId,
            parent.CycleId,
            parent.Title,
            canManage,
            summaries,
            eligible)
        {
            History = history
        };
    }

    public async Task<ProjectTeamResult> CreateAsync(
        Guid projectId,
        string name,
        string actorUserId)
    {
        var cleanName = name?.Trim() ?? string.Empty;

        if (cleanName.Length is < 1 or > 150)
        {
            return Failure(
                "Enter a team name of up to 150 characters.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await CohortWorkflowGate.ForProjectAsync(_db, projectId);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var parent = await GetParentAsync(projectId);

        if (parent is null)
        {
            return Failure("The selected project was not found.");
        }

        if (parent.IsArchived || parent.Mode != ProjectMode.Team)
        {
            return Failure(
                "Teams require an unarchived Team-mode project, cycle, and cohort.");
        }

        if (await _db.ProjectTeams.AnyAsync(team =>
            team.ProjectId == projectId &&
            team.Name == cleanName))
        {
            return Failure(
                "A team with this name already exists in this project.");
        }

        var team = new ProjectTeam
        {
            ProjectId = projectId,
            Name = cleanName,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorUserId
        };

        _db.ProjectTeams.Add(team);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ProjectTeamResult(
                true, Array.Empty<string>());
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627) &&
                  sql.Message.Contains(
                      "IX_ProjectTeams_ProjectId_Name"))
        {
            await transaction.RollbackAsync();
            _db.Entry(team).State = EntityState.Detached;

            return Failure(
                "A team with this name already exists in this project.");
        }
    }

    public async Task<ProjectTeamResult> AssignAsync(
        Guid projectId,
        Guid teamId,
        string candidateId,
        string reason,
        string actorUserId)
    {
        var cleanReason = reason?.Trim() ?? string.Empty;

        if (cleanReason.Length is < 1 or > 1000)
        {
            return Failure(
                "Enter an assignment reason of up to 1000 characters.");
        }

        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return Failure("Select a candidate.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await CohortWorkflowGate.ForProjectAsync(_db, projectId);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var parent = await GetParentAsync(projectId);

        if (parent is null)
        {
            return Failure("The selected project was not found.");
        }

        if (parent.IsArchived || parent.Mode != ProjectMode.Team)
        {
            return Failure(
                "Teams require an unarchived Team-mode project, cycle, and cohort.");
        }

        if (!await _db.ProjectTeams.AnyAsync(team =>
            team.TeamId == teamId &&
            team.ProjectId == projectId))
        {
            return Failure(
                "Select a team belonging to this project.");
        }

        var candidate = await _users.FindByIdAsync(candidateId);

        if (candidate is null ||
            !(await CandidateProgressTracker.Infrastructure.Identity.AccountAvailability.IsActiveAsync(_db, candidate.Id) && await _users.IsInRoleAsync(candidate, "Candidate")))
        {
            return Failure(
                "The selected user must have the Candidate role.");
        }

        var now = DateTimeOffset.UtcNow;

        var eligible = await _db.CohortMemberships.AnyAsync(
            membership =>
                membership.CohortId == parent.CohortId &&
                membership.UserId == candidateId &&
                membership.MembershipKind ==
                    MembershipKind.Candidate &&
                membership.StartsAt <= now &&
                (membership.EndsAt == null ||
                    membership.EndsAt > now));

        if (!eligible)
        {
            return Failure(
                "The candidate must have a current membership in this cohort.");
        }

        var overlap = await _db.TeamMemberships.AnyAsync(
            membership =>
                membership.ProjectId == projectId &&
                membership.UserId == candidateId &&
                (membership.EndsAt == null ||
                    membership.EndsAt > now));

        if (overlap)
        {
            return Failure(
                "The candidate already has an overlapping team membership in this project.");
        }

        var membership = new TeamMembership
        {
            TeamId = teamId,
            ProjectId = projectId,
            UserId = candidateId,
            StartsAt = now,
            ChangeReason = cleanReason,
            ChangedBy = actorUserId
        };

        _db.TeamMemberships.Add(membership);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ProjectTeamResult(
                true, Array.Empty<string>());
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627) &&
                  sql.Message.Contains(
                      "IX_TeamMemberships_ProjectId_UserId"))
        {
            await transaction.RollbackAsync();
            _db.Entry(membership).State = EntityState.Detached;

            return Failure(
                "The candidate already has an open team membership in this project.");
        }
    }

    public async Task<ProjectTeamResult> EndAsync(
        Guid projectId,
        Guid membershipId,
        string rowVersion,
        string reason,
        string actorUserId)
    {
        var cleanReason = reason?.Trim() ?? string.Empty;

        if (cleanReason.Length is < 1 or > 1000)
        {
            return Failure(
                "Enter an ending reason of up to 1000 characters.");
        }

        byte[] expectedVersion;

        try
        {
            expectedVersion = Convert.FromBase64String(
                rowVersion ?? string.Empty);
        }
        catch (FormatException)
        {
            return Failure(
                "Invalid membership version. Refresh the page.");
        }

        if (expectedVersion.Length != 8)
        {
            return Failure(
                "Invalid membership version. Refresh the page.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await CohortWorkflowGate.ForProjectAsync(_db, projectId);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var parent = await GetParentAsync(projectId);

        if (parent is null)
        {
            return Failure("The selected project was not found.");
        }

        if (parent.IsArchived || parent.Mode != ProjectMode.Team)
        {
            return Failure(
                "Team changes require an unarchived Team-mode project, cycle, and cohort.");
        }

        var membership = await _db.TeamMemberships
            .SingleOrDefaultAsync(item =>
                item.TeamMembershipId == membershipId &&
                item.ProjectId == projectId);

        if (membership is null)
        {
            return Failure(
                "The selected membership was not found.");
        }

        if (!membership.RowVersion.SequenceEqual(expectedVersion))
        {
            return Failure(
                "This membership has changed. Refresh the page and try again.");
        }

        if (membership.EndsAt is not null)
        {
            return Failure(
                "This membership already has an ending date.");
        }

        var now = DateTimeOffset.UtcNow;

        if (membership.StartsAt >= now)
        {
            return Failure(
                "Only a membership that has started can be ended.");
        }

        _db.Entry(membership)
            .Property(item => item.RowVersion)
            .OriginalValue = expectedVersion;

        membership.EndsAt = now;
        membership.EndedBy = actorUserId;
        membership.EndReason = cleanReason;

        // Keep the original assignment actor and reason unchanged.
        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ProjectTeamResult(
                true, Array.Empty<string>());
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            _db.Entry(membership).State = EntityState.Detached;

            return Failure(
                "This membership changed while saving. Refresh the page and try again.");
        }
    }
}