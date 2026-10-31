using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class CohortArchiveTests
{
    private record Scenario(
        string AdminId,
        string CandidateId,
        Cohort Cohort,
        CohortMembership Membership);

    private static async Task<Scenario> ArrangeAsync(TestDatabase database)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        async Task<string> CreateUserAsync(string role)
        {
            var user = new IdentityUser
            {
                UserName = $"archive.{Guid.NewGuid():N}"
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

        var cohort = new Cohort
        {
            Name = "Archive test cohort",
            Code = $"ARC-{Guid.NewGuid():N}",
            LocationLabel = "Gqeberha",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedBy = adminId
        };

        var membership = new CohortMembership
        {
            CohortId = cohort.CohortId,
            UserId = candidateId,
            MembershipKind = MembershipKind.Candidate,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            ChangeReason = "Initial assignment",
            CreatedBy = adminId
        };

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        db.Cohorts.Add(cohort);
        db.CohortMemberships.Add(membership);
        await db.SaveChangesAsync();

        return new Scenario(adminId, candidateId, cohort, membership);
    }

    private static async Task<CohortArchiveResult> ArchiveAsync(
        TestDatabase database,
        Scenario scenario,
        string reason,
        byte[] version,
        string actorId)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<ICohortArchiveService>();

        return await service.ArchiveAsync(
            scenario.Cohort.CohortId,
            reason,
            version,
            actorId);
    }

    private static async Task<Cohort> ReadCohortAsync(TestDatabase database)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        return await db.Cohorts.AsNoTracking().SingleAsync();
    }

    private static void AssertNotArchived(Cohort cohort)
    {
        Assert.False(cohort.IsArchived);
        Assert.Null(cohort.ArchivedAt);
        Assert.Null(cohort.ArchivedBy);
        Assert.Null(cohort.ArchiveReason);
    }

    [Fact]
    public async Task Archive_records_audit_details_and_preserves_membership()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var before = DateTimeOffset.UtcNow;

        var result = await ArchiveAsync(
            database,
            scenario,
            "  Programme completed  ",
            scenario.Cohort.RowVersion,
            scenario.AdminId);

        var after = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        var saved = await ReadCohortAsync(database);

        Assert.True(saved.IsArchived);
        Assert.Equal("Programme completed", saved.ArchiveReason);
        Assert.Equal(scenario.AdminId, saved.ArchivedBy);
        Assert.NotNull(saved.ArchivedAt);
        Assert.InRange(saved.ArchivedAt!.Value, before, after);
        Assert.Equal(TimeSpan.Zero, saved.ArchivedAt.Value.Offset);

        Assert.Equal(scenario.Cohort.Name, saved.Name);
        Assert.Equal(scenario.Cohort.Code, saved.Code);
        Assert.Equal(scenario.Cohort.LocationLabel, saved.LocationLabel);
        Assert.Equal(scenario.Cohort.CreatedAt, saved.CreatedAt);
        Assert.Equal(scenario.Cohort.CreatedBy, saved.CreatedBy);
        Assert.False(saved.RowVersion.SequenceEqual(
            scenario.Cohort.RowVersion));

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var membership = await db.CohortMemberships
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(scenario.Membership.MembershipId, membership.MembershipId);
        Assert.Equal(scenario.Membership.CohortId, membership.CohortId);
        Assert.Equal(scenario.Membership.UserId, membership.UserId);
        Assert.Equal(scenario.Membership.MembershipKind, membership.MembershipKind);
        Assert.Equal(scenario.Membership.StartsAt, membership.StartsAt);
        Assert.Equal(scenario.Membership.CreatedBy, membership.CreatedBy);
        Assert.Equal(scenario.Membership.ChangeReason, membership.ChangeReason);

        Assert.Null(membership.EndsAt);
        Assert.Null(membership.EndedAt);
        Assert.Null(membership.EndedBy);
        Assert.Null(membership.EndReason);

        Assert.True(membership.RowVersion.SequenceEqual(
            scenario.Membership.RowVersion));
    }

    [Fact]
    public async Task Candidate_cannot_archive_cohort()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var result = await ArchiveAsync(
            database,
            scenario,
            "Programme completed",
            scenario.Cohort.RowVersion,
            scenario.CandidateId);

        Assert.False(result.Succeeded);
        Assert.Contains("Administrator access is required.", result.Errors);
        AssertNotArchived(await ReadCohortAsync(database));
    }

    [Fact]
    public async Task Blank_reason_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var result = await ArchiveAsync(
            database,
            scenario,
            "   ",
            scenario.Cohort.RowVersion,
            scenario.AdminId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "Enter an archive reason of up to 1000 characters.",
            result.Errors);

        AssertNotArchived(await ReadCohortAsync(database));
    }

    [Fact]
    public async Task Stale_version_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var cohort = await db.Cohorts.SingleAsync();
            cohort.Name = "Updated cohort name";
            await db.SaveChangesAsync();
        }

        var result = await ArchiveAsync(
            database,
            scenario,
            "Programme completed",
            scenario.Cohort.RowVersion,
            scenario.AdminId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "This cohort has changed. Reload the page before trying again.",
            result.Errors);

        var saved = await ReadCohortAsync(database);

        AssertNotArchived(saved);
        Assert.Equal("Updated cohort name", saved.Name);
    }

    [Fact]
    public async Task Repeated_archive_preserves_original_audit_details()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await ArrangeAsync(database);

        var first = await ArchiveAsync(
            database,
            scenario,
            "Original archive reason",
            scenario.Cohort.RowVersion,
            scenario.AdminId);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var archived = await ReadCohortAsync(database);

        var second = await ArchiveAsync(
            database,
            scenario,
            "Replacement reason",
            archived.RowVersion,
            scenario.AdminId);

        Assert.False(second.Succeeded);
        Assert.Contains("This cohort is already archived.", second.Errors);

        var saved = await ReadCohortAsync(database);

        Assert.True(saved.IsArchived);
        Assert.Equal(archived.ArchiveReason, saved.ArchiveReason);
        Assert.Equal(archived.ArchivedAt, saved.ArchivedAt);
        Assert.Equal(archived.ArchivedBy, saved.ArchivedBy);
        Assert.True(saved.RowVersion.SequenceEqual(archived.RowVersion));
    }
}