using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class MilestoneAssignmentTests
{
    private record Setup(
        string AdminId,
        string CandidateId,
        Guid CohortId,
        Guid CycleId,
        Guid ProjectId,
        Guid MilestoneId,
        Guid TeamId);

    private static async Task<string> CreateUserAsync(
        TestDatabase database,
        string role)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser
        {
            UserName = $"assignment.{Guid.NewGuid():N}"
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
        TestDatabase database,
        ProjectMode mode)
    {
        var adminId = await CreateUserAsync(database, "Administrator");
        var candidateId = await CreateUserAsync(database, "Candidate");

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = new Cohort
        {
            Name = "Assignment test cohort",
            Code = $"AT-{Guid.NewGuid():N}",
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
            "Assignment cycle",
            start,
            start.AddMonths(3),
            adminId);

        Assert.True(cycle.Succeeded, string.Join("; ", cycle.Errors));

        var projects = scope.ServiceProvider
            .GetRequiredService<IProjectService>();

        var project = await projects.CreateAsync(
            cycle.CycleId!.Value,
            "Assignment project",
            "Build and test a solution",
            mode,
            adminId);

        Assert.True(project.Succeeded, string.Join("; ", project.Errors));

        var milestones = scope.ServiceProvider
            .GetRequiredService<IMilestoneService>();

        var milestoneResult = await milestones.CreateAsync(
            project.ProjectId!.Value,
            1,
            "Planning",
            "Write the original plan",
            "Include scope and risks",
            "Upload the plan",
            adminId);

        Assert.True(
            milestoneResult.Succeeded,
            string.Join("; ", milestoneResult.Errors));

        var milestoneId = await db.Milestones
            .Where(item => item.ProjectId == project.ProjectId.Value)
            .Select(item => item.MilestoneId)
            .SingleAsync();

        var teamId = Guid.Empty;

        if (mode == ProjectMode.Team)
        {
            var teams = scope.ServiceProvider
                .GetRequiredService<IProjectTeamService>();

            var teamResult = await teams.CreateAsync(
                project.ProjectId.Value, "Alpha", adminId);

            Assert.True(
                teamResult.Succeeded,
                string.Join("; ", teamResult.Errors));

            teamId = await db.ProjectTeams
                .Where(item => item.ProjectId == project.ProjectId.Value)
                .Select(item => item.TeamId)
                .SingleAsync();
        }

        return new Setup(
            adminId,
            candidateId,
            cohort.CohortId,
            cycle.CycleId.Value,
            project.ProjectId.Value,
            milestoneId,
            teamId);
    }

    // Each operation uses a fresh scope, like a separate web request.
    private static async Task<T> CallAsync<T>(
        TestDatabase database,
        Func<IMilestoneAssignmentService, Task<T>> action)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IMilestoneAssignmentService>();

        return await action(service);
    }

    private static void AssertSuccess(MilestoneAssignmentResult result)
    {
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
    }

    private static async Task AssertCountsAsync(
        TestDatabase database,
        int assignments,
        int revisions)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(assignments, await db.Assignments.CountAsync());
        Assert.Equal(revisions, await db.AssignmentRevisions.CountAsync());
    }

    [Fact]
    public async Task Individual_assignment_saves_utc_deadline_and_initial_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database, ProjectMode.Individual);

        var dueAt = DateTimeOffset.UtcNow
            .AddDays(7)
            .ToOffset(TimeSpan.FromHours(2));

        AssertSuccess(await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                setup.CandidateId,
                null,
                dueAt,
                true,
                setup.AdminId)));

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var assignment = await db.Assignments.AsNoTracking().SingleAsync();
            var revision = await db.AssignmentRevisions.AsNoTracking().SingleAsync();

            Assert.Equal(setup.CandidateId, assignment.CandidateId);
            Assert.Null(assignment.TeamId);
            Assert.Equal(setup.CycleId, assignment.CycleId);
            Assert.Equal(setup.ProjectId, assignment.ProjectId);
            Assert.Equal(setup.MilestoneId, assignment.MilestoneId);
            Assert.True(assignment.IsRequired);
            Assert.Equal(setup.AdminId, assignment.CreatedBy);
            Assert.Equal(8, assignment.RowVersion.Length);

            Assert.Equal(dueAt.ToUniversalTime(), assignment.DueAt);
            Assert.Equal(TimeSpan.Zero, assignment.DueAt.Offset);
            Assert.Equal(TimeSpan.Zero, assignment.CreatedAt.Offset);

            Assert.Equal(assignment.AssignmentId, revision.AssignmentId);
            Assert.Equal(1, revision.RevisionNo);
            Assert.Equal(assignment.DueAt, revision.DueAt);
            Assert.Equal(TimeSpan.Zero, revision.DueAt.Offset);
            Assert.Equal(assignment.CreatedAt, revision.ChangedAt);
            Assert.Equal(setup.AdminId, revision.ChangedBy);
            Assert.Equal("Initial assignment", revision.Reason);

            Assert.Equal(
                "Write the original plan",
                revision.InstructionsSnapshot);
            Assert.Equal(
                "Include scope and risks",
                revision.AcceptanceSnapshot);
            Assert.Equal(
                "Upload the plan",
                revision.EvidencePolicySnapshot);

            // Simulate a later definition change directly in the test database.
            var milestone = await db.Milestones.SingleAsync(
                item => item.MilestoneId == setup.MilestoneId);

            milestone.Instructions = "Changed instructions";
            milestone.AcceptanceExpectations = "Changed expectations";
            milestone.EvidencePolicy = "Changed evidence";

            await db.SaveChangesAsync();
        }

        await using (var verification = database.Services.CreateAsyncScope())
        {
            var db = verification.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var revision = await db.AssignmentRevisions
                .AsNoTracking()
                .SingleAsync();

            Assert.Equal(
                "Write the original plan",
                revision.InstructionsSnapshot);
            Assert.Equal(
                "Include scope and risks",
                revision.AcceptanceSnapshot);
            Assert.Equal(
                "Upload the plan",
                revision.EvidencePolicySnapshot);
        }

        var details = await CallAsync(
            database,
            service => service.GetAsync(
                setup.MilestoneId, setup.AdminId));

        Assert.NotNull(details);
        Assert.Single(details.Assignments);
        Assert.DoesNotContain(
            details.Candidates,
            candidate => candidate.UserId == setup.CandidateId);
    }

    [Fact]
    public async Task Team_assignment_supports_empty_team_and_rejects_duplicate()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database, ProjectMode.Team);
        var dueAt = DateTimeOffset.UtcNow.AddDays(7);

        AssertSuccess(await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                null,
                setup.TeamId,
                dueAt,
                false,
                setup.AdminId)));

        var duplicate = await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                null,
                setup.TeamId,
                dueAt.AddDays(1),
                true,
                setup.AdminId));

        Assert.False(duplicate.Succeeded);
        await AssertCountsAsync(database, 1, 1);

        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var assignment = await db.Assignments.AsNoTracking().SingleAsync();

        Assert.Equal(setup.TeamId, assignment.TeamId);
        Assert.Null(assignment.CandidateId);
        Assert.False(assignment.IsRequired);
        Assert.Equal(dueAt, assignment.DueAt);
        Assert.Equal(0, await db.TeamMemberships.CountAsync());

        var details = await CallAsync(
            database,
            service => service.GetAsync(
                setup.MilestoneId, setup.AdminId));

        Assert.NotNull(details);
        Assert.True(details.IsTeamProject);
        Assert.Empty(details.Candidates);
        Assert.Empty(details.Teams);
        Assert.Equal("Alpha", Assert.Single(details.Assignments).OwnerName);
    }

    [Fact]
    public async Task Invalid_owners_deadlines_and_duplicate_individual_are_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database, ProjectMode.Individual);
        var dueAt = DateTimeOffset.UtcNow.AddDays(7);

        var requests = new[]
        {
            (Candidate: (string?)null, Team: (Guid?)null, Due: dueAt),
            (Candidate: (string?)setup.CandidateId,
                Team: (Guid?)Guid.NewGuid(), Due: dueAt),
            (Candidate: (string?)null,
                Team: (Guid?)Guid.Empty, Due: dueAt),
            (Candidate: (string?)null,
                Team: (Guid?)Guid.NewGuid(), Due: dueAt),
            (Candidate: (string?)setup.CandidateId,
                Team: (Guid?)null,
                Due: DateTimeOffset.UtcNow.AddDays(-1))
        };

        foreach (var request in requests)
        {
            var result = await CallAsync(
                database,
                service => service.CreateAsync(
                    setup.MilestoneId,
                    request.Candidate,
                    request.Team,
                    request.Due,
                    true,
                    setup.AdminId));

            Assert.False(result.Succeeded);
        }

        Assert.False((await CallAsync(
            database,
            service => service.CreateAsync(
                Guid.NewGuid(),
                setup.CandidateId,
                null,
                dueAt,
                true,
                setup.AdminId))).Succeeded);

        await AssertCountsAsync(database, 0, 0);

        AssertSuccess(await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                setup.CandidateId,
                null,
                dueAt,
                true,
                setup.AdminId)));

        Assert.False((await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                setup.CandidateId,
                null,
                dueAt.AddDays(1),
                false,
                setup.AdminId))).Succeeded);

        await AssertCountsAsync(database, 1, 1);
    }

    [Fact]
    public async Task Candidate_requires_role_and_current_cohort_membership()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database, ProjectMode.Individual);
        var outsiderId = await CreateUserAsync(database, "Candidate");
        var mentorId = await CreateUserAsync(database, "Mentor");
        var dueAt = DateTimeOffset.UtcNow.AddDays(7);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            // Cohort membership alone does not grant the Candidate role.
            db.CohortMemberships.Add(new CohortMembership
            {
                CohortId = setup.CohortId,
                UserId = mentorId,
                MembershipKind = MembershipKind.Candidate,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                ChangeReason = "Role mismatch test",
                CreatedBy = setup.AdminId
            });

            var membership = await db.CohortMemberships.SingleAsync(
                item => item.UserId == setup.CandidateId);

            membership.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);

            await db.SaveChangesAsync();
        }

        foreach (var candidateId in new[]
        {
            outsiderId,
            mentorId,
            setup.CandidateId
        })
        {
            Assert.False((await CallAsync(
                database,
                service => service.CreateAsync(
                    setup.MilestoneId,
                    candidateId,
                    null,
                    dueAt,
                    true,
                    setup.AdminId))).Succeeded);
        }

        var details = await CallAsync(
            database,
            service => service.GetAsync(
                setup.MilestoneId, setup.AdminId));

        Assert.NotNull(details);
        Assert.Empty(details.Candidates);
        await AssertCountsAsync(database, 0, 0);
    }

    [Fact]
    public async Task Team_project_rejects_candidate_owner_and_team_from_other_project()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database, ProjectMode.Team);
        var dueAt = DateTimeOffset.UtcNow.AddDays(7);
        Guid otherTeamId;

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var projects = scope.ServiceProvider
                .GetRequiredService<IProjectService>();

            var otherProject = await projects.CreateAsync(
                setup.CycleId,
                "Other project",
                "Other project brief",
                ProjectMode.Team,
                setup.AdminId);

            Assert.True(
                otherProject.Succeeded,
                string.Join("; ", otherProject.Errors));

            var teams = scope.ServiceProvider
                .GetRequiredService<IProjectTeamService>();

            var result = await teams.CreateAsync(
                otherProject.ProjectId!.Value,
                "Other team",
                setup.AdminId);

            Assert.True(result.Succeeded, string.Join("; ", result.Errors));

            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            otherTeamId = await db.ProjectTeams
                .Where(team => team.ProjectId == otherProject.ProjectId.Value)
                .Select(team => team.TeamId)
                .SingleAsync();
        }

        Assert.False((await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                setup.CandidateId,
                null,
                dueAt,
                true,
                setup.AdminId))).Succeeded);

        Assert.False((await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                null,
                otherTeamId,
                dueAt,
                true,
                setup.AdminId))).Succeeded);

        await AssertCountsAsync(database, 0, 0);
    }

    [Fact]
    public async Task Nonadministrators_cannot_read_or_create_assignments()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database, ProjectMode.Individual);
        var mentorId = await CreateUserAsync(database, "Mentor");

        foreach (var actorId in new[]
        {
            mentorId,
            setup.CandidateId,
            ""
        })
        {
            Assert.Null(await CallAsync(
                database,
                service => service.GetAsync(
                    setup.MilestoneId, actorId)));

            Assert.False((await CallAsync(
                database,
                service => service.CreateAsync(
                    setup.MilestoneId,
                    setup.CandidateId,
                    null,
                    DateTimeOffset.UtcNow.AddDays(7),
                    true,
                    actorId))).Succeeded);
        }

        await AssertCountsAsync(database, 0, 0);
    }

    [Theory]
    [InlineData("cohort")]
    [InlineData("cycle")]
    [InlineData("project")]
    public async Task Archived_ancestor_blocks_creation(string archiveLevel)
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database, ProjectMode.Individual);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var now = DateTimeOffset.UtcNow;

            if (archiveLevel == "cohort")
            {
                var cohort = await db.Cohorts.SingleAsync(
                    item => item.CohortId == setup.CohortId);

                cohort.IsArchived = true;
                cohort.ArchivedAt = now;
                cohort.ArchivedBy = setup.AdminId;
                cohort.ArchiveReason = "Assignment archive test";
            }
            else if (archiveLevel == "cycle")
            {
                var cycle = await db.LearningCycles.SingleAsync(
                    item => item.CycleId == setup.CycleId);

                cycle.IsArchived = true;
                cycle.ArchivedAt = now;
                cycle.ArchivedBy = setup.AdminId;
                cycle.ArchiveReason = "Assignment archive test";
            }
            else
            {
                var project = await db.Projects.SingleAsync(
                    item => item.ProjectId == setup.ProjectId);

                project.IsArchived = true;
                project.ArchivedAt = now;
                project.ArchivedBy = setup.AdminId;
                project.ArchiveReason = "Assignment archive test";
            }

            await db.SaveChangesAsync();
        }

        Assert.False((await CallAsync(
            database,
            service => service.CreateAsync(
                setup.MilestoneId,
                setup.CandidateId,
                null,
                DateTimeOffset.UtcNow.AddDays(7),
                true,
                setup.AdminId))).Succeeded);

        var details = await CallAsync(
            database,
            service => service.GetAsync(
                setup.MilestoneId, setup.AdminId));

        Assert.NotNull(details);
        Assert.False(details.CanManage);
        Assert.Empty(details.Candidates);
        Assert.Empty(details.Teams);

        await AssertCountsAsync(database, 0, 0);
    }
}