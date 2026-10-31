using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class CohortMembershipEndingTests
{
    private record Scenario(
        string AdminId,
        string CandidateId,
        CohortMembership Original);

    private static async Task<Scenario> ArrangeAsync(TestDatabase database)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        async Task<string> CreateUserAsync(string role)
        {
            var user = new IdentityUser
            {
                UserName = $"ending.{Guid.NewGuid():N}"
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

        var adminId = await CreateUserAsync("Administrator");
        var candidateId = await CreateUserAsync("Candidate");

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = new Cohort
        {
            Name = "Ending test cohort",
            Code = $"END-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedBy = adminId
        };

        var membership = new CohortMembership
        {
            CohortId = cohort.CohortId,
            UserId = candidateId,
            MembershipKind = MembershipKind.Candidate,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            ChangeReason = "Original assignment",
            CreatedBy = adminId
        };

        db.Cohorts.Add(cohort);
        db.CohortMemberships.Add(membership);
        await db.SaveChangesAsync();

        // The scenario scope is disposed before the operation under test.
        return new Scenario(adminId, candidateId, membership);
    }

    private static async Task<MembershipResult> EndAsync(
        TestDatabase database,
        Scenario scenario,
        string reason,
        byte[] version,
        string actorId)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<ICohortMembershipEndingService>();

        return await service.EndAsync(
            scenario.Original.MembershipId,
            reason,
            version,
            actorId);
    }

    private static async Task<CohortMembership> ReadAsync(
        TestDatabase database)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        return await db.CohortMemberships.AsNoTracking().SingleAsync();
    }

    private static void AssertStillOpen(CohortMembership membership)
    {
        Assert.Null(membership.EndsAt);
        Assert.Null(membership.EndedAt);
        Assert.Null(membership.EndedBy);
        Assert.Null(membership.EndReason);
    }

    [Fact]
    public async Task Ending_preserves_assignment_and_records_ending_details()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var before = DateTimeOffset.UtcNow;

        var result = await EndAsync(
            database,
            scenario,
            "  Programme completed  ",
            scenario.Original.RowVersion,
            scenario.AdminId);

        var after = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        var saved = await ReadAsync(database);

        Assert.Equal(scenario.Original.MembershipId, saved.MembershipId);
        Assert.Equal(scenario.Original.CohortId, saved.CohortId);
        Assert.Equal(scenario.CandidateId, saved.UserId);
        Assert.Equal(scenario.Original.MembershipKind, saved.MembershipKind);
        Assert.Equal(scenario.Original.StartsAt, saved.StartsAt);
        Assert.Equal(scenario.Original.CreatedBy, saved.CreatedBy);
        Assert.Equal("Original assignment", saved.ChangeReason);

        Assert.Equal("Programme completed", saved.EndReason);
        Assert.Equal(scenario.AdminId, saved.EndedBy);
        Assert.NotNull(saved.EndsAt);
        Assert.Equal(saved.EndsAt, saved.EndedAt);
        Assert.InRange(saved.EndedAt!.Value, before, after);
        Assert.True(saved.EndsAt!.Value > saved.StartsAt);
        Assert.False(saved.RowVersion.SequenceEqual(
            scenario.Original.RowVersion));
    }

    [Fact]
    public async Task Stale_version_is_rejected_without_ending_membership()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        // Simulate an edit after the administrator opened the page.
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var membership = await db.CohortMemberships.SingleAsync();
            membership.ChangeReason = "Corrected assignment reason";
            await db.SaveChangesAsync();
        }

        var result = await EndAsync(
            database,
            scenario,
            "Programme completed",
            scenario.Original.RowVersion,
            scenario.AdminId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "This membership has changed. Reload the page before trying again.",
            result.Errors);

        var saved = await ReadAsync(database);

        AssertStillOpen(saved);
        Assert.Equal("Corrected assignment reason", saved.ChangeReason);
    }

    [Fact]
    public async Task Candidate_cannot_end_membership()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var result = await EndAsync(
            database,
            scenario,
            "Leaving cohort",
            scenario.Original.RowVersion,
            scenario.CandidateId);

        Assert.False(result.Succeeded);
        Assert.Contains("Administrator access is required.", result.Errors);
        AssertStillOpen(await ReadAsync(database));
    }

    [Fact]
    public async Task Blank_reason_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var result = await EndAsync(
            database,
            scenario,
            "   ",
            scenario.Original.RowVersion,
            scenario.AdminId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "Enter an ending reason of up to 1000 characters.",
            result.Errors);

        AssertStillOpen(await ReadAsync(database));
    }

    [Fact]
    public async Task Archived_cohort_membership_cannot_be_ended()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var cohort = await db.Cohorts.SingleAsync();
            cohort.IsArchived = true;
            await db.SaveChangesAsync();
        }

        var result = await EndAsync(
            database,
            scenario,
            "Programme completed",
            scenario.Original.RowVersion,
            scenario.AdminId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "Memberships cannot be changed in an archived cohort.",
            result.Errors);

        AssertStillOpen(await ReadAsync(database));
    }

    [Fact]
    public async Task Ending_again_does_not_overwrite_original_ending()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var first = await EndAsync(
            database,
            scenario,
            "First ending reason",
            scenario.Original.RowVersion,
            scenario.AdminId);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var ended = await ReadAsync(database);

        // Use the latest version to test the already-ended rule,
        // rather than triggering the stale-version rule.
        var second = await EndAsync(
            database,
            scenario,
            "Replacement reason",
            ended.RowVersion,
            scenario.AdminId);

        Assert.False(second.Succeeded);
        Assert.Contains(
            "This membership already has an end date.",
            second.Errors);

        var saved = await ReadAsync(database);

        Assert.Equal("First ending reason", saved.EndReason);
        Assert.Equal(ended.EndsAt, saved.EndsAt);
        Assert.Equal(ended.EndedAt, saved.EndedAt);
        Assert.Equal(ended.EndedBy, saved.EndedBy);
        Assert.True(saved.RowVersion.SequenceEqual(ended.RowVersion));
    }

    [Fact]
    public async Task Reassignment_creates_new_membership_and_preserves_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var endingResult = await EndAsync(
            database,
            scenario,
            "Initial participation completed",
            scenario.Original.RowVersion,
            scenario.AdminId);

        Assert.True(
            endingResult.Succeeded,
            string.Join("; ", endingResult.Errors));

        var ended = await ReadAsync(database);

        MembershipResult reassignment;

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<ICohortMembershipService>();

            reassignment = await service.AssignAsync(
                ended.CohortId,
                ended.UserId,
                ended.MembershipKind,
                "Rejoined cohort for additional training",
                scenario.AdminId);
        }

        Assert.True(
            reassignment.Succeeded,
            string.Join("; ", reassignment.Errors));

        Assert.NotNull(reassignment.MembershipId);
        Assert.NotEqual(ended.MembershipId, reassignment.MembershipId!.Value);

        await using var verificationScope =
            database.Services.CreateAsyncScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var records = await db.CohortMemberships
            .AsNoTracking()
            .Where(item =>
                item.CohortId == ended.CohortId &&
                item.UserId == ended.UserId &&
                item.MembershipKind == ended.MembershipKind)
            .ToListAsync();

        Assert.Equal(2, records.Count);

        var historical = Assert.Single(
            records,
            item => item.MembershipId == ended.MembershipId);

        Assert.Equal(ended.StartsAt, historical.StartsAt);
        Assert.Equal(ended.EndsAt, historical.EndsAt);
        Assert.Equal(ended.CreatedBy, historical.CreatedBy);
        Assert.Equal(ended.ChangeReason, historical.ChangeReason);
        Assert.Equal(ended.EndedBy, historical.EndedBy);
        Assert.Equal(ended.EndedAt, historical.EndedAt);
        Assert.Equal(ended.EndReason, historical.EndReason);
        Assert.True(ended.RowVersion.SequenceEqual(historical.RowVersion));

        var active = Assert.Single(
            records,
            item => item.EndsAt == null);

        Assert.Equal(reassignment.MembershipId!.Value, active.MembershipId);
        Assert.True(active.StartsAt >= ended.EndsAt!.Value);
        Assert.Equal(scenario.AdminId, active.CreatedBy);
        Assert.Equal(
            "Rejoined cohort for additional training",
            active.ChangeReason);

        Assert.Null(active.EndedBy);
        Assert.Null(active.EndedAt);
        Assert.Null(active.EndReason);
    }
}