using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class CohortCreationTests
{
    private const string TestPassword = "Integration-Only!482";

    private static async Task<string> CreateActorAsync(
        TestDatabase database,
        string role)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser
        {
            UserName = $"actor.{Guid.NewGuid():N}",
            LockoutEnabled = true
        };

        var created = await users.CreateAsync(user, TestPassword);

        Assert.True(
            created.Succeeded,
            string.Join("; ", created.Errors.Select(error => error.Description)));

        var assigned = await users.AddToRoleAsync(user, role);

        Assert.True(
            assigned.Succeeded,
            string.Join("; ", assigned.Errors.Select(error => error.Description)));

        return user.Id;
    }

    private static async Task<CohortCreationResult> CreateCohortAsync(
        TestDatabase database,
        string name,
        string code,
        string? location,
        string actorId)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<ICohortCreationService>();

        return await service.CreateAsync(name, code, location, actorId);
    }

    [Fact]
    public async Task Administrator_creates_normalised_cohort_with_audit_fields()
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = await CreateActorAsync(database, "Administrator");

        var before = DateTimeOffset.UtcNow;

        var result = await CreateCohortAsync(
            database,
            "  QA Cohort 2026  ",
            "  gqe-2026-01  ",
            "  Gqeberha  ",
            actorId);

        var after = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.NotNull(result.CohortId);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = await db.Cohorts.AsNoTracking().SingleAsync();

        Assert.Equal(result.CohortId!.Value, cohort.CohortId);
        Assert.Equal("QA Cohort 2026", cohort.Name);
        Assert.Equal("GQE-2026-01", cohort.Code);
        Assert.Equal("Gqeberha", cohort.LocationLabel);
        Assert.Equal(actorId, cohort.CreatedBy);
        Assert.False(cohort.IsArchived);
        Assert.InRange(cohort.CreatedAt, before, after);
        Assert.Equal(TimeSpan.Zero, cohort.CreatedAt.Offset);
        Assert.Equal(8, cohort.RowVersion.Length);
    }

    [Fact]
    public async Task Blank_optional_location_is_saved_as_null()
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = await CreateActorAsync(database, "Administrator");

        var result = await CreateCohortAsync(
            database, "Dev Cohort", "DEV-01", "   ", actorId);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = await db.Cohorts.AsNoTracking().SingleAsync();

        Assert.Null(cohort.LocationLabel);
    }

    [Theory]
    [InlineData("Candidate")]
    [InlineData("Mentor")]
    public async Task Non_administrator_cannot_create_cohort(string role)
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = await CreateActorAsync(database, role);

        var result = await CreateCohortAsync(
            database, "QA Cohort", "QA-01", null, actorId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "Administrator access is required.",
            result.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.Cohorts.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonexistent-user-id")]
    public async Task Missing_or_unknown_actor_cannot_create_cohort(
        string actorId)
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await CreateCohortAsync(
            database, "QA Cohort", "QA-01", null, actorId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "Administrator access is required.",
            result.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.Cohorts.CountAsync());
    }

    [Theory]
    [InlineData("   ", "QA-01")]
    [InlineData("QA Cohort", "   ")]
    public async Task Required_fields_cannot_be_whitespace(
        string name,
        string code)
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = await CreateActorAsync(database, "Administrator");

        var result = await CreateCohortAsync(
            database, name, code, null, actorId);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.Cohorts.CountAsync());
    }

    [Fact]
    public async Task Duplicate_normalised_code_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = await CreateActorAsync(database, "Administrator");

        var first = await CreateCohortAsync(
            database, "First cohort", "GQE-01", null, actorId);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var second = await CreateCohortAsync(
            database, "Second cohort", "  gqe-01  ", null, actorId);

        Assert.False(second.Succeeded);
        Assert.Contains(
            "A cohort with this code already exists.",
            second.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = await db.Cohorts.AsNoTracking().SingleAsync();

        Assert.Equal("First cohort", cohort.Name);
    }
}