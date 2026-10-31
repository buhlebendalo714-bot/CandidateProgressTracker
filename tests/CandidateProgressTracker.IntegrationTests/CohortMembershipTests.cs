using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class CohortMembershipTests
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
            UserName = $"member.{Guid.NewGuid():N}"
        };

        var created = await users.CreateAsync(
            user, "Integration-Only!482");

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

        var service = scope.ServiceProvider
            .GetRequiredService<ICohortCreationService>();

        var result = await service.CreateAsync(
            "Membership test cohort",
            $"TEST-{Guid.NewGuid():N}",
            null,
            adminId);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.NotNull(result.CohortId);

        return result.CohortId!.Value;
    }

    private static async Task<MembershipResult> AssignAsync(
        TestDatabase database,
        Guid cohortId,
        string memberId,
        MembershipKind kind,
        string actorId)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<ICohortMembershipService>();

        return await service.AssignAsync(
            cohortId,
            memberId,
            kind,
            "  Initial assignment  ",
            actorId);
    }

    [Theory]
    [InlineData("Candidate", MembershipKind.Candidate)]
    [InlineData("Mentor", MembershipKind.Mentor)]
    public async Task Administrator_assigns_eligible_member(
        string role,
        MembershipKind kind)
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var memberId = await CreateUserAsync(database, role);
        var cohortId = await CreateCohortAsync(database, adminId);

        var before = DateTimeOffset.UtcNow;

        var result = await AssignAsync(
            database, cohortId, memberId, kind, adminId);

        var after = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.NotNull(result.MembershipId);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var membership = await db.CohortMemberships
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(result.MembershipId!.Value, membership.MembershipId);
        Assert.Equal(cohortId, membership.CohortId);
        Assert.Equal(memberId, membership.UserId);
        Assert.Equal(kind, membership.MembershipKind);
        Assert.Equal(adminId, membership.CreatedBy);
        Assert.Equal("Initial assignment", membership.ChangeReason);
        Assert.Null(membership.EndsAt);
        Assert.InRange(membership.StartsAt, before, after);
        Assert.Equal(TimeSpan.Zero, membership.StartsAt.Offset);
        Assert.Equal(8, membership.RowVersion.Length);
    }

    [Theory]
    [InlineData("Candidate")]
    [InlineData("Mentor")]
    public async Task Non_administrator_cannot_assign_members(string actorRole)
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var actorId = await CreateUserAsync(database, actorRole);
        var memberId = await CreateUserAsync(database, "Candidate");
        var cohortId = await CreateCohortAsync(database, adminId);

        var result = await AssignAsync(
            database,
            cohortId,
            memberId,
            MembershipKind.Candidate,
            actorId);

        Assert.False(result.Succeeded);
        Assert.Contains("Administrator access is required.", result.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.CohortMemberships.CountAsync());
    }

    [Fact]
    public async Task Candidate_cannot_be_assigned_as_mentor()
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var memberId = await CreateUserAsync(database, "Candidate");
        var cohortId = await CreateCohortAsync(database, adminId);

        var result = await AssignAsync(
            database,
            cohortId,
            memberId,
            MembershipKind.Mentor,
            adminId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "The selected user must have the Mentor role.",
            result.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.CohortMemberships.CountAsync());
    }

    [Fact]
    public async Task Archived_cohort_rejects_new_members()
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var memberId = await CreateUserAsync(database, "Candidate");
        var cohortId = await CreateCohortAsync(database, adminId);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var cohort = await db.Cohorts.SingleAsync();
            cohort.IsArchived = true;
            await db.SaveChangesAsync();
        }

        var result = await AssignAsync(
            database,
            cohortId,
            memberId,
            MembershipKind.Candidate,
            adminId);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "Members cannot be added to an archived cohort.",
            result.Errors);

        await using var verificationScope =
            database.Services.CreateAsyncScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await verificationDb.CohortMemberships.CountAsync());
    }

    [Fact]
    public async Task Duplicate_open_membership_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var memberId = await CreateUserAsync(database, "Candidate");
        var cohortId = await CreateCohortAsync(database, adminId);

        var first = await AssignAsync(
            database,
            cohortId,
            memberId,
            MembershipKind.Candidate,
            adminId);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var second = await AssignAsync(
            database,
            cohortId,
            memberId,
            MembershipKind.Candidate,
            adminId);

        Assert.False(second.Succeeded);
        Assert.Contains(
            "This user already has an overlapping membership " +
            "of this kind in the cohort.",
            second.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var membership = await db.CohortMemberships
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(first.MembershipId!.Value, membership.MembershipId);
    }
}