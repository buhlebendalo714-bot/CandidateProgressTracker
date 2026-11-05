using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class AssignmentRevisionTests
{
    private record Setup(
        string AdminId,
        string CandidateId,
        Guid CohortId,
        Guid CycleId,
        Guid ProjectId,
        Guid AssignmentId);

    private static async Task<string> CreateUserAsync(
        TestDatabase database,
        string role)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser
        {
            UserName = $"revision.{Guid.NewGuid():N}"
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

    private static async Task<Setup> ArrangeAsync(TestDatabase database)
    {
        var adminId = await CreateUserAsync(database, "Administrator");
        var candidateId = await CreateUserAsync(database, "Candidate");

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var now = DateTimeOffset.UtcNow;

        var cohort = new Cohort
        {
            Name = "Revision cohort",
            Code = $"R-{Guid.NewGuid():N}",
            CreatedAt = now,
            CreatedBy = adminId
        };

        db.Cohorts.Add(cohort);
        await db.SaveChangesAsync();

        db.CohortMemberships.Add(new CohortMembership
        {
            CohortId = cohort.CohortId,
            UserId = candidateId,
            MembershipKind = MembershipKind.Candidate,
            StartsAt = now.AddDays(-1),
            ChangeReason = "Revision test setup",
            CreatedBy = adminId
        });

        await db.SaveChangesAsync();

        var cycles = scope.ServiceProvider
            .GetRequiredService<ILearningCycleService>();

        var start = DateOnly.FromDateTime(now.UtcDateTime);

        var cycle = await cycles.CreateAsync(
            cohort.CohortId,
            "Revision cycle",
            start,
            start.AddMonths(3),
            adminId);

        Assert.True(cycle.Succeeded, string.Join("; ", cycle.Errors));

        var projects = scope.ServiceProvider
            .GetRequiredService<IProjectService>();

        var project = await projects.CreateAsync(
            cycle.CycleId!.Value,
            "Revision project",
            "Project brief",
            ProjectMode.Individual,
            adminId);

        Assert.True(project.Succeeded, string.Join("; ", project.Errors));

        var milestones = scope.ServiceProvider
            .GetRequiredService<IMilestoneService>();

        var milestoneResult = await milestones.CreateAsync(
            project.ProjectId!.Value,
            1,
            "Planning",
            "Original instructions",
            "Original expectations",
            "Original evidence",
            adminId);

        Assert.True(
            milestoneResult.Succeeded,
            string.Join("; ", milestoneResult.Errors));

        var milestoneId = await db.Milestones
            .Where(item => item.ProjectId == project.ProjectId.Value)
            .Select(item => item.MilestoneId)
            .SingleAsync();

        var assignments = scope.ServiceProvider
            .GetRequiredService<IMilestoneAssignmentService>();

        var result = await assignments.CreateAsync(
            milestoneId,
            candidateId,
            null,
            now.AddDays(7),
            true,
            adminId);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        var assignmentId = await db.Assignments
            .Select(item => item.AssignmentId)
            .SingleAsync();

        return new Setup(
            adminId,
            candidateId,
            cohort.CohortId,
            cycle.CycleId.Value,
            project.ProjectId.Value,
            assignmentId);
    }

    // Every call uses a separate scope, like a fresh web request.
    private static async Task<T> CallAsync<T>(
        TestDatabase database,
        Func<IAssignmentRevisionService, Task<T>> action)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IAssignmentRevisionService>();

        return await action(service);
    }

    private static async Task<AssignmentRevisionDetails> DetailsAsync(
        TestDatabase database,
        Setup setup)
    {
        var result = await CallAsync(
            database,
            service => service.GetAsync(
                setup.AssignmentId, setup.AdminId));

        Assert.NotNull(result);
        return result!;
    }

    private static Task<AssignmentRevisionResult> ReviseAsync(
        TestDatabase database,
        Setup setup,
        AssignmentRevisionDetails current,
        DateTimeOffset? dueAt = null,
        string? instructions = null,
        string? acceptance = null,
        string? evidence = null,
        string reason = "Test revision",
        string? actorId = null,
        string? version = null)
    {
        return CallAsync(
            database,
            service => service.ReviseAsync(
                setup.AssignmentId,
                version ?? current.RowVersion,
                dueAt ?? current.DueAt,
                instructions ?? current.Instructions,
                acceptance ?? current.AcceptanceExpectations,
                evidence ?? current.EvidencePolicy,
                reason,
                actorId ?? setup.AdminId));
    }

    private static async Task AssertUnchangedAsync(
        TestDatabase database,
        Setup setup,
        AssignmentRevisionDetails before)
    {
        var after = await DetailsAsync(database, setup);

        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(before.RevisionNo, after.RevisionNo);
        Assert.Equal(before.DueAt, after.DueAt);
        Assert.Equal(before.Instructions, after.Instructions);
        Assert.Equal(
            before.AcceptanceExpectations,
            after.AcceptanceExpectations);
        Assert.Equal(before.EvidencePolicy, after.EvidencePolicy);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(1, await db.AssignmentRevisions.CountAsync());
    }

    [Fact]
    public async Task Revision_preserves_original_and_updates_deadline_and_snapshots()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var before = await DetailsAsync(database, setup);

        var newDeadline = before.DueAt
            .AddDays(3)
            .ToOffset(TimeSpan.FromHours(2));

        var result = await ReviseAsync(
            database,
            setup,
            before,
            dueAt: newDeadline,
            instructions: "  Revised instructions  ",
            acceptance: "  Revised expectations  ",
            evidence: "  Revised evidence  ",
            reason: "  Deadline extension and clarified scope  ");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        var after = await DetailsAsync(database, setup);

        Assert.Equal(2, after.RevisionNo);
        Assert.NotEqual(before.RowVersion, after.RowVersion);
        Assert.Equal(newDeadline.ToUniversalTime(), after.DueAt);
        Assert.Equal(TimeSpan.Zero, after.DueAt.Offset);
        Assert.Equal("Revised instructions", after.Instructions);
        Assert.Equal("Revised expectations", after.AcceptanceExpectations);
        Assert.Equal("Revised evidence", after.EvidencePolicy);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var revisions = await db.AssignmentRevisions.AsNoTracking()
            .OrderBy(item => item.RevisionNo)
            .ToListAsync();

        Assert.Equal(2, revisions.Count);

        var original = Assert.Single(
            revisions, item => item.RevisionNo == 1);

        Assert.Equal(before.DueAt, original.DueAt);
        Assert.Equal(before.Instructions, original.InstructionsSnapshot);
        Assert.Equal(
            before.AcceptanceExpectations,
            original.AcceptanceSnapshot);
        Assert.Equal(before.EvidencePolicy, original.EvidencePolicySnapshot);
        Assert.Equal("Initial assignment", original.Reason);

        var revised = Assert.Single(
            revisions, item => item.RevisionNo == 2);

        Assert.Equal(setup.AdminId, revised.ChangedBy);
        Assert.Equal(
            "Deadline extension and clarified scope",
            revised.Reason);
        Assert.Equal(TimeSpan.Zero, revised.ChangedAt.Offset);

        var assignment = await db.Assignments.AsNoTracking().SingleAsync();

        Assert.Equal(revised.DueAt, assignment.DueAt);
        Assert.Equal(setup.AdminId, assignment.CreatedBy);
        Assert.Equal(original.ChangedAt, assignment.CreatedAt);

        // Definition remains unchanged: this revision belongs to one assignment.
        var milestone = await db.Milestones.AsNoTracking().SingleAsync();

        Assert.Equal("Original instructions", milestone.Instructions);
    }

    [Fact]
    public async Task Requirements_only_revision_advances_version_and_rejects_stale_form()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var before = await DetailsAsync(database, setup);

        var first = await ReviseAsync(
            database,
            setup,
            before,
            instructions: "Clarified instructions");

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var current = await DetailsAsync(database, setup);

        Assert.Equal(before.DueAt, current.DueAt);
        Assert.NotEqual(before.RowVersion, current.RowVersion);
        Assert.Equal(2, current.RevisionNo);

        // Simulate a second tab submitting its old version token.
        var stale = await ReviseAsync(
            database,
            setup,
            before,
            instructions: "Stale overwrite");

        Assert.False(stale.Succeeded);

        var afterStale = await DetailsAsync(database, setup);

        Assert.Equal(current.RowVersion, afterStale.RowVersion);
        Assert.Equal("Clarified instructions", afterStale.Instructions);
        Assert.Equal(2, afterStale.RevisionNo);

        // A fresh read permits the next intentional edit.
        var next = await ReviseAsync(
            database,
            setup,
            current,
            evidence: "Updated evidence requirements");

        Assert.True(next.Succeeded, string.Join("; ", next.Errors));

        var final = await DetailsAsync(database, setup);

        Assert.Equal(3, final.RevisionNo);
        Assert.Equal("Clarified instructions", final.Instructions);
        Assert.Equal("Updated evidence requirements", final.EvidencePolicy);

        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(3, await db.AssignmentRevisions.CountAsync());
    }

    [Fact]
    public async Task Invalid_fields_versions_and_no_change_do_not_write()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var before = await DetailsAsync(database, setup);

        // A reason alone does not create a revision.
        Assert.False((await ReviseAsync(
            database, setup, before)).Succeeded);

        foreach (var version in new[]
        {
            "",
            "not-base64!",
            Convert.ToBase64String(new byte[7]),
            Convert.ToBase64String(new byte[8])
        })
        {
            Assert.False((await ReviseAsync(
                database,
                setup,
                before,
                instructions: "Changed instructions",
                version: version)).Succeeded);
        }

        foreach (var instructions in new[] { " ", new string('x', 10001) })
        {
            Assert.False((await ReviseAsync(
                database,
                setup,
                before,
                instructions: instructions)).Succeeded);
        }

        foreach (var acceptance in new[] { " ", new string('x', 10001) })
        {
            Assert.False((await ReviseAsync(
                database,
                setup,
                before,
                acceptance: acceptance)).Succeeded);
        }

        foreach (var evidence in new[] { " ", new string('x', 2001) })
        {
            Assert.False((await ReviseAsync(
                database,
                setup,
                before,
                evidence: evidence)).Succeeded);
        }

        foreach (var reason in new[] { " ", new string('x', 1001) })
        {
            Assert.False((await ReviseAsync(
                database,
                setup,
                before,
                instructions: "Changed instructions",
                reason: reason)).Succeeded);
        }

        await AssertUnchangedAsync(database, setup, before);
    }

    [Fact]
    public async Task Changed_deadline_cannot_be_in_the_past()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var before = await DetailsAsync(database, setup);

        var result = await ReviseAsync(
            database,
            setup,
            before,
            dueAt: DateTimeOffset.UtcNow.AddDays(-1));

        Assert.False(result.Succeeded);
        await AssertUnchangedAsync(database, setup, before);
    }

    [Fact]
    public async Task Requirements_can_change_while_retaining_existing_past_deadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var pastDeadline = DateTimeOffset.UtcNow.AddDays(-1);

        // Arrange an assignment whose deadline has already elapsed.
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var assignment = await db.Assignments.SingleAsync();
            var revision = await db.AssignmentRevisions.SingleAsync();

            assignment.DueAt = pastDeadline;
            revision.DueAt = pastDeadline;

            await db.SaveChangesAsync();
        }

        var before = await DetailsAsync(database, setup);

        var result = await ReviseAsync(
            database,
            setup,
            before,
            instructions: "Clarification without extending the deadline");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        var after = await DetailsAsync(database, setup);

        Assert.Equal(2, after.RevisionNo);
        Assert.Equal(pastDeadline, after.DueAt);
        Assert.NotEqual(before.RowVersion, after.RowVersion);
    }

    [Fact]
    public async Task Nonadministrators_cannot_read_or_revise()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var mentorId = await CreateUserAsync(database, "Mentor");
        var before = await DetailsAsync(database, setup);

        foreach (var actorId in new[]
        {
            setup.CandidateId,
            mentorId,
            "",
            Guid.NewGuid().ToString()
        })
        {
            Assert.Null(await CallAsync(
                database,
                service => service.GetAsync(
                    setup.AssignmentId, actorId)));

            Assert.False((await ReviseAsync(
                database,
                setup,
                before,
                instructions: "Unauthorized change",
                actorId: actorId)).Succeeded);
        }

        await AssertUnchangedAsync(database, setup, before);
    }

    [Theory]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("cohort")]
    public async Task Archived_ancestor_blocks_revision(string level)
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var before = await DetailsAsync(database, setup);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var now = DateTimeOffset.UtcNow;

            if (level == "project")
            {
                var project = await db.Projects.SingleAsync();

                project.IsArchived = true;
                project.ArchivedAt = now;
                project.ArchivedBy = setup.AdminId;
                project.ArchiveReason = "Revision archive test";
            }
            else if (level == "cycle")
            {
                var cycle = await db.LearningCycles.SingleAsync();

                cycle.IsArchived = true;
                cycle.ArchivedAt = now;
                cycle.ArchivedBy = setup.AdminId;
                cycle.ArchiveReason = "Revision archive test";
            }
            else
            {
                var cohort = await db.Cohorts.SingleAsync();

                cohort.IsArchived = true;
                cohort.ArchivedAt = now;
                cohort.ArchivedBy = setup.AdminId;
                cohort.ArchiveReason = "Revision archive test";
            }

            await db.SaveChangesAsync();
        }

        var details = await DetailsAsync(database, setup);
        Assert.False(details.CanManage);

        Assert.False((await ReviseAsync(
            database,
            setup,
            before,
            instructions: "Blocked revision")).Succeeded);

        await AssertUnchangedAsync(database, setup, before);
    }

    [Fact]
    public async Task History_returns_saved_revisions_and_remains_readable_after_archive()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var before = await DetailsAsync(database, setup);

        var revised = await ReviseAsync(
            database,
            setup,
            before,
            instructions: "History test instructions",
            reason: "Clarified the instructions");

        Assert.True(revised.Succeeded, string.Join("; ", revised.Errors));

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<IAssignmentHistoryQueryService>();

            var history = await service.GetAsync(
                setup.AssignmentId, setup.AdminId);

            Assert.NotNull(history);
            Assert.False(history.IsArchived);
            Assert.False(history.HasNextPage);

            Assert.Equal(
                new[] { 2, 1 },
                history.Revisions.Select(item => item.RevisionNo).ToArray());

            var latest = history.Revisions[0];

            Assert.Equal("History test instructions", latest.Instructions);
            Assert.Equal("Clarified the instructions", latest.Reason);
            Assert.Equal(setup.AdminId, latest.ChangedBy);
            Assert.False(string.IsNullOrWhiteSpace(latest.ChangedByName));

            var original = history.Revisions[1];

            Assert.Equal(before.Instructions, original.Instructions);
            Assert.Equal(
                before.AcceptanceExpectations,
                original.AcceptanceExpectations);
            Assert.Equal(before.EvidencePolicy, original.EvidencePolicy);
            Assert.Equal(before.DueAt, original.DueAt);
            Assert.Equal("Initial assignment", original.Reason);

            Assert.Null(await service.GetAsync(
                setup.AssignmentId, setup.CandidateId));

            Assert.Null(await service.GetAsync(
                Guid.NewGuid(), setup.AdminId));

            Assert.Null(await service.GetAsync(
                setup.AssignmentId, setup.AdminId, 0));

            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var cohort = await db.Cohorts.SingleAsync();

            cohort.IsArchived = true;
            cohort.ArchivedAt = DateTimeOffset.UtcNow;
            cohort.ArchivedBy = setup.AdminId;
            cohort.ArchiveReason = "History visibility test";

            await db.SaveChangesAsync();
        }

        await using var verification = database.Services.CreateAsyncScope();

        var historyService = verification.ServiceProvider
            .GetRequiredService<IAssignmentHistoryQueryService>();

        var archivedHistory = await historyService.GetAsync(
            setup.AssignmentId, setup.AdminId);

        Assert.NotNull(archivedHistory);
        Assert.True(archivedHistory.IsArchived);
        Assert.Equal(2, archivedHistory.Revisions.Count);
        Assert.Equal(
            "History test instructions",
            archivedHistory.Revisions[0].Instructions);
        Assert.Equal(
            before.Instructions,
            archivedHistory.Revisions[1].Instructions);
    }

    [Fact]
    public async Task History_paginates_without_duplicates_and_is_scoped_to_assignment()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            // Arrange retained history directly to test pagination.
            var original = await db.AssignmentRevisions
                .AsNoTracking()
                .SingleAsync();

            for (var revisionNo = 2; revisionNo <= 23; revisionNo++)
            {
                db.AssignmentRevisions.Add(new AssignmentRevision
                {
                    AssignmentId = setup.AssignmentId,
                    RevisionNo = revisionNo,
                    DueAt = original.DueAt,
                    InstructionsSnapshot = $"Instructions {revisionNo}",
                    AcceptanceSnapshot = original.AcceptanceSnapshot,
                    EvidencePolicySnapshot = original.EvidencePolicySnapshot,
                    ChangedAt = DateTimeOffset.UtcNow,
                    ChangedBy = setup.AdminId,
                    Reason = $"Revision {revisionNo}"
                });
            }

            await db.SaveChangesAsync();

            // Create a second owner's assignment on the same milestone.
            var candidateId = await CreateUserAsync(database, "Candidate");

            db.CohortMemberships.Add(new CohortMembership
            {
                CohortId = setup.CohortId,
                UserId = candidateId,
                MembershipKind = MembershipKind.Candidate,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                ChangeReason = "Second owner",
                CreatedBy = setup.AdminId
            });

            await db.SaveChangesAsync();

            var milestoneId = await db.Assignments
                .Where(item => item.AssignmentId == setup.AssignmentId)
                .Select(item => item.MilestoneId)
                .SingleAsync();

            var assignments = scope.ServiceProvider
                .GetRequiredService<IMilestoneAssignmentService>();

            var created = await assignments.CreateAsync(
                milestoneId,
                candidateId,
                null,
                DateTimeOffset.UtcNow.AddDays(7),
                true,
                setup.AdminId);

            Assert.True(created.Succeeded, string.Join("; ", created.Errors));

            var otherRevision = await db.AssignmentRevisions.SingleAsync(
                item => item.AssignmentId != setup.AssignmentId);

            otherRevision.InstructionsSnapshot = "OTHER OWNER PRIVATE REQUIREMENTS";
            await db.SaveChangesAsync();
        }

        await using var verification = database.Services.CreateAsyncScope();

        var service = verification.ServiceProvider
            .GetRequiredService<IAssignmentHistoryQueryService>();

        var first = await service.GetAsync(
            setup.AssignmentId, setup.AdminId, 1);

        var second = await service.GetAsync(
            setup.AssignmentId, setup.AdminId, 2);

        Assert.NotNull(first);
        Assert.NotNull(second);

        Assert.Equal(20, first.Revisions.Count);
        Assert.Equal(3, second.Revisions.Count);
        Assert.True(first.HasNextPage);
        Assert.False(second.HasNextPage);

        var combined = first.Revisions.Concat(second.Revisions).ToArray();

        Assert.Equal(
            Enumerable.Range(1, 23).Reverse().ToArray(),
            combined.Select(item => item.RevisionNo).ToArray());

        Assert.DoesNotContain(
            combined,
            item => item.Instructions == "OTHER OWNER PRIVATE REQUIREMENTS");
    }
}