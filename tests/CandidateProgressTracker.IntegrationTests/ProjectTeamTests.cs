using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class ProjectTeamTests
{
    private record Setup(
        string AdminId,
        string CandidateId,
        Guid CohortId,
        Guid CycleId,
        Guid ProjectId);

    private static async Task<string> CreateUserAsync(
        TestDatabase database,
        string role)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser
        {
            UserName = $"teams.{Guid.NewGuid():N}"
        };

        var created = await users.CreateAsync(
            user, "Integration-Only!482");

        Assert.True(
            created.Succeeded,
            string.Join("; ",
                created.Errors.Select(error => error.Description)));

        var assigned = await users.AddToRoleAsync(user, role);

        Assert.True(
            assigned.Succeeded,
            string.Join("; ",
                assigned.Errors.Select(error => error.Description)));

        return user.Id;
    }

    private static async Task<Setup> ArrangeAsync(
        TestDatabase database)
    {
        var adminId = await CreateUserAsync(database, "Administrator");
        var candidateId = await CreateUserAsync(database, "Candidate");

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = new Cohort
        {
            Name = "Team test cohort",
            Code = $"TT-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = adminId
        };

        db.Cohorts.Add(cohort);
        await db.SaveChangesAsync();

        db.CohortMemberships.Add(new CohortMembership
        {
            CohortId = cohort.CohortId,
            UserId = candidateId,
            MembershipKind = MembershipKind.Candidate,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            ChangeReason = "Test cohort assignment",
            CreatedBy = adminId
        });

        await db.SaveChangesAsync();

        var cycles = scope.ServiceProvider
            .GetRequiredService<ILearningCycleService>();

        var start = DateOnly.FromDateTime(DateTime.UtcNow);

        var cycle = await cycles.CreateAsync(
            cohort.CohortId,
            "Team cycle",
            start,
            start.AddMonths(3),
            adminId);

        Assert.True(cycle.Succeeded, string.Join("; ", cycle.Errors));

        var projects = scope.ServiceProvider
            .GetRequiredService<IProjectService>();

        var project = await projects.CreateAsync(
            cycle.CycleId!.Value,
            "Team project",
            "Build and test a solution",
            ProjectMode.Team,
            adminId);

        Assert.True(project.Succeeded, string.Join("; ", project.Errors));

        return new Setup(
            adminId,
            candidateId,
            cohort.CohortId,
            cycle.CycleId.Value,
            project.ProjectId!.Value);
    }

    // Each call uses a fresh scope, like a separate web request.
    private static async Task<T> CallAsync<T>(
        TestDatabase database,
        Func<IProjectTeamService, Task<T>> action)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IProjectTeamService>();

        return await action(service);
    }

    private static async Task<ProjectTeamDetails> DetailsAsync(
        TestDatabase database,
        Setup setup)
    {
        var details = await CallAsync(
            database,
            service => service.GetAsync(
                setup.ProjectId, setup.AdminId));

        Assert.NotNull(details);
        return details!;
    }

    private static async Task<Guid> CreateTeamAsync(
        TestDatabase database,
        Setup setup,
        string name)
    {
        var result = await CallAsync(
            database,
            service => service.CreateAsync(
                setup.ProjectId, name, setup.AdminId));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        var details = await DetailsAsync(database, setup);

        return Assert.Single(
            details.Teams,
            team => team.Name == name.Trim()).TeamId;
    }

    private static void AssertSuccess(ProjectTeamResult result)
    {
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task End_and_reassign_preserves_both_memberships_and_audit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        var firstTeam = await CreateTeamAsync(database, setup, "  Alpha  ");
        var secondTeam = await CreateTeamAsync(database, setup, "Beta");

        AssertSuccess(await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId,
                firstTeam,
                setup.CandidateId,
                "  Original assignment  ",
                setup.AdminId)));

        var assigned = await DetailsAsync(database, setup);
        var member = Assert.Single(
            assigned.Teams.Single(team => team.TeamId == firstTeam).Members);

        Assert.Equal(setup.CandidateId, member.UserId);
        Assert.Equal(8, Convert.FromBase64String(member.RowVersion).Length);
        Assert.DoesNotContain(
            assigned.EligibleCandidates,
            candidate => candidate.UserId == setup.CandidateId);

        AssertSuccess(await CallAsync(
            database,
            service => service.EndAsync(
                setup.ProjectId,
                member.TeamMembershipId,
                member.RowVersion,
                "  Moving to Beta  ",
                setup.AdminId)));

        var ended = await DetailsAsync(database, setup);

        Assert.Empty(
            ended.Teams.Single(team => team.TeamId == firstTeam).Members);

        Assert.Contains(
            ended.EligibleCandidates,
            candidate => candidate.UserId == setup.CandidateId);

        AssertSuccess(await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId,
                secondTeam,
                setup.CandidateId,
                "New assignment",
                setup.AdminId)));

        var reassigned = await DetailsAsync(database, setup);

        Assert.Equal(2, reassigned.History.Count);
        Assert.Single(
            reassigned.Teams.Single(team => team.TeamId == secondTeam).Members);

        await using var verification = database.Services.CreateAsyncScope();
        var db = verification.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var rows = await db.TeamMemberships.AsNoTracking()
            .Where(item => item.ProjectId == setup.ProjectId)
            .ToListAsync();

        Assert.Equal(2, rows.Count);

        var original = Assert.Single(
            rows, item => item.TeamId == firstTeam);

        Assert.Equal("Original assignment", original.ChangeReason);
        Assert.Equal(setup.AdminId, original.ChangedBy);
        Assert.Equal("Moving to Beta", original.EndReason);
        Assert.Equal(setup.AdminId, original.EndedBy);
        Assert.NotNull(original.EndsAt);
        Assert.True(original.EndsAt!.Value > original.StartsAt);
        Assert.NotEqual(
            member.RowVersion,
            Convert.ToBase64String(original.RowVersion));

        var current = Assert.Single(
            rows, item => item.TeamId == secondTeam);

        Assert.Null(current.EndsAt);
        Assert.Null(current.EndedBy);
        Assert.Null(current.EndReason);
        Assert.Equal("New assignment", current.ChangeReason);
    }

    [Fact]
    public async Task Invalid_creation_and_ineligible_or_duplicate_assignments_are_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var outsiderId = await CreateUserAsync(database, "Candidate");
        var mentorId = await CreateUserAsync(database, "Mentor");

        var alpha = await CreateTeamAsync(database, setup, "Alpha");
        var beta = await CreateTeamAsync(database, setup, "Beta");

        foreach (var name in new[] { " ", new string('x', 151), "Alpha" })
        {
            Assert.False((await CallAsync(
                database,
                service => service.CreateAsync(
                    setup.ProjectId, name, setup.AdminId))).Succeeded);
        }

        // Candidate role alone is insufficient without cohort membership.
        foreach (var userId in new[] { outsiderId, mentorId })
        {
            Assert.False((await CallAsync(
                database,
                service => service.AssignAsync(
                    setup.ProjectId, alpha, userId,
                    "Assignment", setup.AdminId))).Succeeded);
        }

        Assert.False((await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, alpha, setup.CandidateId,
                " ", setup.AdminId))).Succeeded);

        Guid otherTeam;
        Guid individualProject;

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var projects = scope.ServiceProvider
                .GetRequiredService<IProjectService>();

            var other = await projects.CreateAsync(
                setup.CycleId, "Other team project", "Brief",
                ProjectMode.Team, setup.AdminId);

            Assert.True(other.Succeeded, string.Join("; ", other.Errors));

            var otherSetup = setup with
            {
                ProjectId = other.ProjectId!.Value
            };

            otherTeam = await CreateTeamAsync(
                database, otherSetup, "Other team");

            var individual = await projects.CreateAsync(
                setup.CycleId, "Individual project", "Brief",
                ProjectMode.Individual, setup.AdminId);

            Assert.True(
                individual.Succeeded,
                string.Join("; ", individual.Errors));

            individualProject = individual.ProjectId!.Value;
        }

        Assert.False((await CallAsync(
            database,
            service => service.CreateAsync(
                individualProject, "Invalid team", setup.AdminId))).Succeeded);

        Assert.False((await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, otherTeam, setup.CandidateId,
                "Wrong project", setup.AdminId))).Succeeded);

        AssertSuccess(await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, alpha, setup.CandidateId,
                "First assignment", setup.AdminId)));

        Assert.False((await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, beta, setup.CandidateId,
                "Duplicate assignment", setup.AdminId))).Succeeded);

        await using var verification = database.Services.CreateAsyncScope();
        var db = verification.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(2, await db.ProjectTeams.CountAsync(
            team => team.ProjectId == setup.ProjectId));

        Assert.Equal(1, await db.TeamMemberships.CountAsync());
    }

    [Fact]
    public async Task Ending_rejects_invalid_and_stale_requests_without_overwriting_audit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var teamId = await CreateTeamAsync(database, setup, "Alpha");

        AssertSuccess(await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, teamId, setup.CandidateId,
                "Original assignment", setup.AdminId)));

        var before = await DetailsAsync(database, setup);
        var member = Assert.Single(Assert.Single(before.Teams).Members);

        foreach (var version in new[]
        {
            "",
            "not-base64!",
            Convert.ToBase64String(new byte[7]),
            Convert.ToBase64String(new byte[8])
        })
        {
            Assert.False((await CallAsync(
                database,
                service => service.EndAsync(
                    setup.ProjectId, member.TeamMembershipId,
                    version, "End membership", setup.AdminId))).Succeeded);
        }

        Assert.False((await CallAsync(
            database,
            service => service.EndAsync(
                setup.ProjectId, member.TeamMembershipId,
                member.RowVersion, " ", setup.AdminId))).Succeeded);

        Assert.False((await CallAsync(
            database,
            service => service.EndAsync(
                setup.ProjectId, Guid.NewGuid(),
                member.RowVersion, "Unknown membership",
                setup.AdminId))).Succeeded);

        // All rejected attempts must leave membership open.
        var unchanged = await DetailsAsync(database, setup);
        Assert.Single(Assert.Single(unchanged.Teams).Members);
        Assert.Null(Assert.Single(unchanged.History).EndsAt);

        AssertSuccess(await CallAsync(
            database,
            service => service.EndAsync(
                setup.ProjectId, member.TeamMembershipId,
                member.RowVersion, "First ending", setup.AdminId)));

        // A second browser tab submits the original rowversion.
        var stale = await CallAsync(
            database,
            service => service.EndAsync(
                setup.ProjectId, member.TeamMembershipId,
                member.RowVersion, "Stale overwrite", setup.AdminId));

        Assert.False(stale.Succeeded);

        string currentVersion;

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var saved = await db.TeamMemberships.AsNoTracking().SingleAsync();

            Assert.Equal("First ending", saved.EndReason);
            Assert.Equal("Original assignment", saved.ChangeReason);
            Assert.Equal(setup.AdminId, saved.EndedBy);

            currentVersion = Convert.ToBase64String(saved.RowVersion);
        }

        // Even a fresh version cannot end an already-ended membership again.
        Assert.False((await CallAsync(
            database,
            service => service.EndAsync(
                setup.ProjectId, member.TeamMembershipId,
                currentVersion, "Second ending", setup.AdminId))).Succeeded);

        var final = await DetailsAsync(database, setup);
        Assert.Equal("First ending", Assert.Single(final.History).EndReason);
    }

    [Fact]
    public async Task Nonadministrators_cannot_read_create_assign_or_end()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var mentorId = await CreateUserAsync(database, "Mentor");
        var teamId = await CreateTeamAsync(database, setup, "Alpha");

        AssertSuccess(await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, teamId, setup.CandidateId,
                "Original assignment", setup.AdminId)));

        var details = await DetailsAsync(database, setup);
        var member = Assert.Single(Assert.Single(details.Teams).Members);

        foreach (var actor in new[] { mentorId, setup.CandidateId, "" })
        {
            Assert.Null(await CallAsync(
                database,
                service => service.GetAsync(setup.ProjectId, actor)));

            Assert.False((await CallAsync(
                database,
                service => service.CreateAsync(
                    setup.ProjectId, "Unauthorized", actor))).Succeeded);

            Assert.False((await CallAsync(
                database,
                service => service.AssignAsync(
                    setup.ProjectId, teamId, setup.CandidateId,
                    "Unauthorized", actor))).Succeeded);

            Assert.False((await CallAsync(
                database,
                service => service.EndAsync(
                    setup.ProjectId, member.TeamMembershipId,
                    member.RowVersion, "Unauthorized", actor))).Succeeded);
        }

        var final = await DetailsAsync(database, setup);

        Assert.Single(final.Teams);
        Assert.Single(Assert.Single(final.Teams).Members);
        Assert.Null(Assert.Single(final.History).EndsAt);
    }

    [Fact]
    public async Task Archived_cohort_blocks_all_team_writes_but_keeps_history_visible()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var teamId = await CreateTeamAsync(database, setup, "Alpha");

        AssertSuccess(await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, teamId, setup.CandidateId,
                "Original assignment", setup.AdminId)));

        var before = await DetailsAsync(database, setup);
        var member = Assert.Single(Assert.Single(before.Teams).Members);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var cohort = await db.Cohorts.SingleAsync(
                item => item.CohortId == setup.CohortId);

            cohort.IsArchived = true;
            cohort.ArchivedAt = DateTimeOffset.UtcNow;
            cohort.ArchivedBy = setup.AdminId;
            cohort.ArchiveReason = "Archive test";
            await db.SaveChangesAsync();
        }

        Assert.False((await CallAsync(
            database,
            service => service.CreateAsync(
                setup.ProjectId, "Blocked", setup.AdminId))).Succeeded);

        Assert.False((await CallAsync(
            database,
            service => service.AssignAsync(
                setup.ProjectId, teamId, setup.CandidateId,
                "Blocked", setup.AdminId))).Succeeded);

        Assert.False((await CallAsync(
            database,
            service => service.EndAsync(
                setup.ProjectId, member.TeamMembershipId,
                member.RowVersion, "Blocked", setup.AdminId))).Succeeded);

        var after = await DetailsAsync(database, setup);

        Assert.False(after.CanManage);
        Assert.Empty(after.EligibleCandidates);
        Assert.Single(after.Teams);
        Assert.Single(Assert.Single(after.Teams).Members);
        Assert.Null(Assert.Single(after.History).EndsAt);
    }
}