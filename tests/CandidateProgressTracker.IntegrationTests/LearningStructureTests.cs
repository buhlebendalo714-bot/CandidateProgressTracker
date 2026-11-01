using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class LearningStructureTests
{
    private static async Task<string> CreateUserAsync(
        TestDatabase database,
        string role)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser
        {
            UserName = $"learning.{Guid.NewGuid():N}"
        };

        var created = await users.CreateAsync(user, "Integration-Only!482");

        Assert.True(
            created.Succeeded,
            string.Join("; ", created.Errors.Select(error => error.Description)));

        var assigned = await users.AddToRoleAsync(user, role);

        Assert.True(
            assigned.Succeeded,
            string.Join("; ", assigned.Errors.Select(error => error.Description)));

        return user.Id;
    }

    private static async Task<Guid> CreateCohortAsync(
        TestDatabase database,
        string adminId)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = new Cohort
        {
            Name = "Learning test cohort",
            Code = $"LT-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = adminId
        };

        db.Cohorts.Add(cohort);
        await db.SaveChangesAsync();

        return cohort.CohortId;
    }

    [Fact]
    public async Task Administrator_can_create_structure_and_invalid_inputs_are_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = await CreateUserAsync(database, "Administrator");
        var cohortId = await CreateCohortAsync(database, adminId);

        await using var scope = database.Services.CreateAsyncScope();

        var cycles = scope.ServiceProvider
            .GetRequiredService<ILearningCycleService>();
        var projects = scope.ServiceProvider
            .GetRequiredService<IProjectService>();
        var milestones = scope.ServiceProvider
            .GetRequiredService<IMilestoneService>();

        var start = new DateOnly(2026, 11, 1);
        var end = start.AddMonths(3);

        var invalidDates = await cycles.CreateAsync(
            cohortId, "Invalid dates", end, start, adminId);
        Assert.False(invalidDates.Succeeded);

        var cycle = await cycles.CreateAsync(
            cohortId, "  Testing cycle  ", start, end, adminId);
        Assert.True(cycle.Succeeded, string.Join("; ", cycle.Errors));
        Assert.NotNull(cycle.CycleId);

        var duplicateCycle = await cycles.CreateAsync(
            cohortId, "Testing cycle", start, end, adminId);
        Assert.False(duplicateCycle.Succeeded);

        var invalidMode = await projects.CreateAsync(
            cycle.CycleId.Value, "Invalid mode", "Brief",
            (ProjectMode)99, adminId);
        Assert.False(invalidMode.Succeeded);

        var project = await projects.CreateAsync(
            cycle.CycleId.Value, "  Test automation  ", "  Build a suite  ",
            ProjectMode.Individual, adminId);
        Assert.True(project.Succeeded, string.Join("; ", project.Errors));
        Assert.NotNull(project.ProjectId);

        var second = await milestones.CreateAsync(
            project.ProjectId.Value, 2, "Execution",
            "Run the suite", "Results are reproducible",
            "Execution report", adminId);
        Assert.True(second.Succeeded, string.Join("; ", second.Errors));

        var first = await milestones.CreateAsync(
            project.ProjectId.Value, 1, "Planning",
            "Write a test plan", "Scope and risks are documented",
            "Test plan document", adminId);
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var duplicateSequence = await milestones.CreateAsync(
            project.ProjectId.Value, 1, "Duplicate",
            "Instructions", "Expectations", "Evidence", adminId);
        Assert.False(duplicateSequence.Succeeded);

        var blankInstructions = await milestones.CreateAsync(
            project.ProjectId.Value, 3, "Invalid",
            "   ", "Expectations", "Evidence", adminId);
        Assert.False(blankInstructions.Succeeded);

        // Verify persisted results through a fresh context.
        await using var verification = database.Services.CreateAsyncScope();

        var db = verification.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var savedCycle = await db.LearningCycles.AsNoTracking().SingleAsync();
        Assert.Equal("Testing cycle", savedCycle.Name);
        Assert.Equal(adminId, savedCycle.CreatedBy);

        var savedProject = await db.Projects.AsNoTracking().SingleAsync();
        Assert.Equal("Test automation", savedProject.Title);
        Assert.Equal("Build a suite", savedProject.Brief);
        Assert.Equal(ProjectMode.Individual, savedProject.Mode);
        Assert.Equal(savedCycle.CycleId, savedProject.CycleId);
        Assert.Equal(adminId, savedProject.CreatedBy);

        var query = verification.ServiceProvider
            .GetRequiredService<IMilestoneService>();

        var details = await query.GetForProjectAsync(
            savedProject.ProjectId, adminId);

        Assert.NotNull(details);
        Assert.Equal(
            new[] { 1, 2 },
            details.Milestones.Select(item => item.SequenceNo).ToArray());

        var savedMilestones = await db.Milestones.AsNoTracking().ToListAsync();
        Assert.All(savedMilestones, item =>
        {
            Assert.Equal(adminId, item.CreatedBy);
            Assert.Equal(8, item.RowVersion.Length);
        });
    }

    [Fact]
    public async Task Nonadministrators_and_archived_cohorts_block_creation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = await CreateUserAsync(database, "Administrator");
        var mentorId = await CreateUserAsync(database, "Mentor");
        var candidateId = await CreateUserAsync(database, "Candidate");
        var cohortId = await CreateCohortAsync(database, adminId);

        await using var scope = database.Services.CreateAsyncScope();

        var cycles = scope.ServiceProvider
            .GetRequiredService<ILearningCycleService>();
        var projects = scope.ServiceProvider
            .GetRequiredService<IProjectService>();
        var milestones = scope.ServiceProvider
            .GetRequiredService<IMilestoneService>();

        var start = new DateOnly(2026, 11, 1);
        var end = start.AddMonths(3);

        var cycle = await cycles.CreateAsync(
            cohortId, "Cycle", start, end, adminId);
        Assert.True(cycle.Succeeded, string.Join("; ", cycle.Errors));

        var project = await projects.CreateAsync(
            cycle.CycleId!.Value, "Project", "Brief",
            ProjectMode.Team, adminId);
        Assert.True(project.Succeeded, string.Join("; ", project.Errors));

        foreach (var actorId in new[] { mentorId, candidateId })
        {
            Assert.False((await cycles.CreateAsync(
                cohortId, "Unauthorized", start, end, actorId)).Succeeded);

            Assert.False((await projects.CreateAsync(
                cycle.CycleId.Value, "Unauthorized", "Brief",
                ProjectMode.Individual, actorId)).Succeeded);

            Assert.False((await milestones.CreateAsync(
                project.ProjectId!.Value, 1, "Unauthorized",
                "Instructions", "Expectations", "Evidence",
                actorId)).Succeeded);
        }

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = await db.Cohorts.SingleAsync();
        cohort.IsArchived = true;
        cohort.ArchivedAt = DateTimeOffset.UtcNow;
        cohort.ArchivedBy = adminId;
        cohort.ArchiveReason = "Test archive";
        await db.SaveChangesAsync();

        Assert.False((await cycles.CreateAsync(
            cohortId, "After archive", start, end, adminId)).Succeeded);

        Assert.False((await projects.CreateAsync(
            cycle.CycleId.Value, "After archive", "Brief",
            ProjectMode.Individual, adminId)).Succeeded);

        Assert.False((await milestones.CreateAsync(
            project.ProjectId!.Value, 1, "After archive",
            "Instructions", "Expectations", "Evidence",
            adminId)).Succeeded);

        Assert.Equal(1, await db.LearningCycles.CountAsync());
        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.Equal(0, await db.Milestones.CountAsync());
    }

    [Fact]
    public async Task Cycle_visibility_requires_current_membership_and_hides_archived_cycles()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = await CreateUserAsync(database, "Administrator");
        var mentorId = await CreateUserAsync(database, "Mentor");
        var candidateId = await CreateUserAsync(database, "Candidate");
        var cohortId = await CreateCohortAsync(database, adminId);

        await using var scope = database.Services.CreateAsyncScope();

        var cycles = scope.ServiceProvider
            .GetRequiredService<ILearningCycleService>();
        var query = scope.ServiceProvider
            .GetRequiredService<IMemberCycleQueryService>();
        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var start = new DateOnly(2026, 11, 1);
        var cycle = await cycles.CreateAsync(
            cohortId, "Visible cycle", start, start.AddMonths(3), adminId);
        Assert.True(cycle.Succeeded, string.Join("; ", cycle.Errors));

        var members = new[]
        {
            (UserId: mentorId, Kind: MembershipKind.Mentor),
            (UserId: candidateId, Kind: MembershipKind.Candidate)
        };

        foreach (var member in members)
        {
            Assert.Null(await query.GetAsync(
                cohortId, member.UserId, member.Kind));

            db.CohortMemberships.Add(new CohortMembership
            {
                CohortId = cohortId,
                UserId = member.UserId,
                MembershipKind = member.Kind,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                ChangeReason = "Test assignment",
                CreatedBy = adminId
            });
        }

        await db.SaveChangesAsync();

        foreach (var member in members)
        {
            var visible = await query.GetAsync(
                cohortId, member.UserId, member.Kind);

            Assert.NotNull(visible);
            Assert.Equal(cycle.CycleId!.Value,
                Assert.Single(visible.Cycles).CycleId);
        }

        Assert.Null(await query.GetAsync(
            cohortId, candidateId, MembershipKind.Mentor));

        var savedCycle = await db.LearningCycles.SingleAsync();
        savedCycle.IsArchived = true;
        savedCycle.ArchivedAt = DateTimeOffset.UtcNow;
        savedCycle.ArchivedBy = adminId;
        savedCycle.ArchiveReason = "Test archive";
        await db.SaveChangesAsync();

        foreach (var member in members)
        {
            var visible = await query.GetAsync(
                cohortId, member.UserId, member.Kind);

            Assert.NotNull(visible);
            Assert.Empty(visible.Cycles);
        }

        var memberships = await db.CohortMemberships.ToListAsync();

        foreach (var membership in memberships)
        {
            membership.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        }

        await db.SaveChangesAsync();

        foreach (var member in members)
        {
            Assert.Null(await query.GetAsync(
                cohortId, member.UserId, member.Kind));
        }
    }
}