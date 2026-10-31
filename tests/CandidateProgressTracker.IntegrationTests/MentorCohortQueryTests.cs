using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class MentorCohortQueryTests
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
            UserName = $"mentor-query.{Guid.NewGuid():N}"
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

    private static async Task<Guid> AddCohortAsync(
        TestDatabase database,
        string adminId,
        string memberId,
        string name,
        MembershipKind kind,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt = null,
        bool archived = false)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var cohort = new Cohort
        {
            Name = name,
            Code = $"MQ-{Guid.NewGuid():N}",
            LocationLabel = "Gqeberha",
            CreatedAt = startsAt.AddDays(-1),
            CreatedBy = adminId,
            IsArchived = archived,
            ArchivedAt = archived ? DateTimeOffset.UtcNow : null,
            ArchivedBy = archived ? adminId : null,
            ArchiveReason = archived ? "Programme completed" : null
        };

        db.Cohorts.Add(cohort);

        db.CohortMemberships.Add(new CohortMembership
        {
            CohortId = cohort.CohortId,
            UserId = memberId,
            MembershipKind = kind,
            StartsAt = startsAt,
            EndsAt = endsAt,
            ChangeReason = "Query test assignment",
            CreatedBy = adminId
        });

        await db.SaveChangesAsync();

        return cohort.CohortId;
    }

    private static async Task<IReadOnlyList<CohortSummary>> QueryAsync(
        TestDatabase database,
        string userId)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var query = scope.ServiceProvider
            .GetRequiredService<IMentorCohortQueryService>();

        return await query.GetMyCohortsAsync(userId);
    }

    [Fact]
    public async Task Returns_only_own_current_unarchived_mentor_cohorts()
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var mentorId = await CreateUserAsync(database, "Mentor");
        var otherMentorId = await CreateUserAsync(database, "Mentor");
        var now = DateTimeOffset.UtcNow;

        var openId = await AddCohortAsync(
            database, adminId, mentorId,
            "Alpha cohort", MembershipKind.Mentor,
            now.AddDays(-2));

        var scheduledEndId = await AddCohortAsync(
            database, adminId, mentorId,
            "Beta cohort", MembershipKind.Mentor,
            now.AddDays(-2), now.AddDays(2));

        var otherMentorCohortId = await AddCohortAsync(
            database, adminId, otherMentorId,
            "Another mentor's cohort", MembershipKind.Mentor,
            now.AddDays(-2));

        await AddCohortAsync(
            database, adminId, mentorId,
            "Ended assignment", MembershipKind.Mentor,
            now.AddDays(-3), now.AddDays(-1));

        await AddCohortAsync(
            database, adminId, mentorId,
            "Future assignment", MembershipKind.Mentor,
            now.AddDays(2));

        await AddCohortAsync(
            database, adminId, mentorId,
            "Archived cohort", MembershipKind.Mentor,
            now.AddDays(-2), archived: true);

        // Deliberately seed a different membership kind to verify
        // that a global Mentor role alone does not grant visibility.
        await AddCohortAsync(
            database, adminId, mentorId,
            "Candidate membership", MembershipKind.Candidate,
            now.AddDays(-2));

        // Historical membership must not duplicate the current cohort.
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            db.CohortMemberships.Add(new CohortMembership
            {
                CohortId = openId,
                UserId = mentorId,
                MembershipKind = MembershipKind.Mentor,
                StartsAt = now.AddDays(-10),
                EndsAt = now.AddDays(-5),
                ChangeReason = "Earlier assignment",
                CreatedBy = adminId
            });

            await db.SaveChangesAsync();
        }

        var results = await QueryAsync(database, mentorId);

        Assert.Equal(
            new[] { openId, scheduledEndId },
            results.Select(cohort => cohort.CohortId).ToArray());

        Assert.All(results, cohort =>
        {
            Assert.False(cohort.IsArchived);
            Assert.Equal("Gqeberha", cohort.LocationLabel);
        });

        var otherResults = await QueryAsync(database, otherMentorId);

        var otherCohort = Assert.Single(otherResults);
        Assert.Equal(otherMentorCohortId, otherCohort.CohortId);
    }

    [Fact]
    public async Task Removing_mentor_role_hides_existing_assignments()
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var mentorId = await CreateUserAsync(database, "Mentor");

        var cohortId = await AddCohortAsync(
            database, adminId, mentorId,
            "Assigned cohort", MembershipKind.Mentor,
            DateTimeOffset.UtcNow.AddDays(-1));

        var before = Assert.Single(await QueryAsync(database, mentorId));
        Assert.Equal(cohortId, before.CohortId);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            var user = await users.FindByIdAsync(mentorId);
            Assert.NotNull(user);

            var removed = await users.RemoveFromRoleAsync(user, "Mentor");

            Assert.True(
                removed.Succeeded,
                string.Join("; ",
                    removed.Errors.Select(error => error.Description)));
        }

        Assert.Empty(await QueryAsync(database, mentorId));
    }

    [Fact]
    public async Task Unassigned_unknown_and_blank_users_receive_no_cohorts()
    {
        await using var database = await TestDatabase.CreateAsync();

        var adminId = await CreateUserAsync(database, "Administrator");
        var assignedMentorId = await CreateUserAsync(database, "Mentor");
        var unassignedMentorId = await CreateUserAsync(database, "Mentor");

        await AddCohortAsync(
            database, adminId, assignedMentorId,
            "Existing cohort", MembershipKind.Mentor,
            DateTimeOffset.UtcNow.AddDays(-1));

        Assert.Empty(await QueryAsync(database, unassignedMentorId));
        Assert.Empty(await QueryAsync(database, Guid.NewGuid().ToString()));
        Assert.Empty(await QueryAsync(database, string.Empty));
        Assert.Empty(await QueryAsync(database, "   "));
    }
}