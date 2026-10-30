using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class AccountCreationTests
{
    private const string ValidPassword = "Integration-Only!482";

    [Theory]
    [InlineData("Candidate")]
    [InlineData("Mentor")]
    public async Task Allowed_role_creates_account_with_expected_role(
        string role)
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.CreateAccountAsync(
            "test.user", ValidPassword, role);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        // Verify through a fresh scope, after the transaction committed.
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = await users.FindByNameAsync("test.user");

        Assert.NotNull(user);

        var roles = await users.GetRolesAsync(user!);

        Assert.Single(roles);
        Assert.Contains(role, roles);
        Assert.True(await users.CheckPasswordAsync(user!, ValidPassword));
    }

    [Fact]
    public async Task Administrator_role_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.CreateAccountAsync(
            "test.user", ValidPassword, "Administrator");

        Assert.False(result.Succeeded);
        Assert.Contains("Select Candidate or Mentor.", result.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Duplicate_username_is_rejected_case_insensitively()
    {
        await using var database = await TestDatabase.CreateAsync();

        var first = await database.CreateAccountAsync(
            "duplicate.user", ValidPassword, "Candidate");

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var second = await database.CreateAccountAsync(
            "DUPLICATE.USER", ValidPassword, "Mentor");

        Assert.False(second.Succeeded);
        Assert.NotEmpty(second.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        Assert.Equal(1, await users.Users.CountAsync());

        var user = await users.FindByNameAsync("duplicate.user");
        Assert.NotNull(user);

        var roles = await users.GetRolesAsync(user!);

        Assert.Single(roles);
        Assert.Contains("Candidate", roles);
    }

    [Theory]
    [InlineData("Short1!")]
    [InlineData("alllowercasepassword")]
    public async Task Invalid_password_creates_no_account(string password)
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.CreateAccountAsync(
            "test.user", password, "Candidate");

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Missing_role_rolls_back_account_creation()
    {
        await using var database = await TestDatabase.CreateAsync();

        // Remove Mentor only from this disposable database.
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            var mentor = await roles.FindByNameAsync("Mentor");
            Assert.NotNull(mentor);

            var deletion = await roles.DeleteAsync(mentor!);
            Assert.True(deletion.Succeeded);
        }

        // Identity's EF store throws when the requested role is missing.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await database.CreateAccountAsync(
                "rollback.user", ValidPassword, "Mentor");
        });

        // Verify persisted state using a new context.
        await using var verificationScope =
            database.Services.CreateAsyncScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.UserRoles.CountAsync());
    }
}