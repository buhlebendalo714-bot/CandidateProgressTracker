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
}