using System.Net;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class AdministratorServiceAuthorizationTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("unknown")]
    [InlineData("Candidate")]
    [InlineData("Mentor")]
    [InlineData("revoked")]
    public async Task Admin_services_deny_missing_or_noncurrent_administrator_without_data_or_writes(string scenario)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var db = s.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var users = s.GetRequiredService<UserManager<IdentityUser>>();
        var actor = scenario == "anonymous" ? "" : scenario == "unknown" ? Guid.NewGuid().ToString() :
            scenario == "Candidate" ? seed.CandidateId : (await CreateAdministratorAsync(users)).Id;
        if (scenario is "revoked" or "Mentor")
        {
            var user = (await users.FindByIdAsync(actor))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "Administrator")).Succeeded);
            if (scenario == "Mentor")
            {
                Assert.True((await users.AddToRoleAsync(user, "Mentor")).Succeeded);
                db.CohortMemberships.Add(new() { CohortId = seed.CohortId, UserId = actor, MembershipKind = MembershipKind.Mentor,
                    StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedBy = seed.AdministratorId, ChangeReason = "Scoped mentor access" });
                await db.SaveChangesAsync();
                Assert.NotNull(await s.GetRequiredService<IMentorCohortQueryService>().GetDetailsAsync(seed.CohortId, actor));
            }
        }
        if (scenario == "Candidate")
            Assert.Contains(await s.GetRequiredService<ICandidateCohortQueryService>().GetMyCohortsAsync(actor), c => c.CohortId == seed.CohortId);
        var before = await db.Users.CountAsync();
        Assert.Null(await s.GetRequiredService<IAccountQueryService>().GetAccountsAsync(actor));
        var cohorts = s.GetRequiredService<ICohortQueryService>();
        Assert.Null(await cohorts.GetCohortsAsync(actor));
        Assert.Null(await cohorts.GetAsync(seed.CohortId, actor));
        Assert.Null(await s.GetRequiredService<ICohortMembershipQueryService>().GetAsync(seed.CohortId, actor));
        var result = await s.GetRequiredService<IAccountCreationService>().CreateAsync("denied." + Guid.NewGuid().ToString("N"), "Integration-Only!482", "Candidate", actor);
        Assert.False(result.Succeeded); Assert.Contains(result.Errors, e => e.Contains("Administrator"));
        Assert.Equal(before, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Current_administrator_can_create_accounts_and_read_admin_queries()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var s = scope.ServiceProvider; var db = s.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var name = "allowed." + Guid.NewGuid().ToString("N");
        var result = await s.GetRequiredService<IAccountCreationService>().CreateAsync(name, "Integration-Only!482", "Candidate", seed.AdministratorId);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Contains((await s.GetRequiredService<IAccountQueryService>().GetAccountsAsync(seed.AdministratorId))!, a => a.UserName == name);
        var cohorts = s.GetRequiredService<ICohortQueryService>();
        Assert.Contains((await cohorts.GetCohortsAsync(seed.AdministratorId))!, c => c.CohortId == seed.CohortId);
        Assert.NotNull(await cohorts.GetAsync(seed.CohortId, seed.AdministratorId));
        var members = await s.GetRequiredService<ICohortMembershipQueryService>().GetAsync(seed.CohortId, seed.AdministratorId);
        Assert.NotNull(members); Assert.NotEmpty(members.Memberships);
    }

    [Fact]
    public async Task Stale_administrator_cookie_cannot_read_admin_data_or_create_an_account_after_role_removal()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var s = scope.ServiceProvider; var db = s.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var users = s.GetRequiredService<UserManager<IdentityUser>>();
        var actor = await CreateAdministratorAsync(users);
        using var client = await fixture.CreateClientAsync("Administrator", actor.Id);
        using var form = await client.GetAsync("/Admin/Users/Create");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        SubmissionPageTests.CopyResponseCookies(client, form);
        var token = SubmissionPageTests.Hidden(await form.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        Assert.True((await users.RemoveFromRoleAsync(actor, "Administrator")).Succeeded);
        // Lifecycle policy now rejects cookies whose role claims no longer match
        // current roles, before a Razor handler or its service can execute.
        foreach (var route in new[] { "/Admin/Users", "/Admin/Cohorts",
                     $"/Admin/Cohorts/Members/{seed.CohortId}", $"/Admin/Cohorts/Archive/{seed.CohortId}" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
            Assert.DoesNotContain("Submission test cohort", await response.Content.ReadAsStringAsync());
        }
        var name = "stale." + Guid.NewGuid().ToString("N");
        var before = await db.Users.CountAsync();
        using var posted = await client.PostAsync("/Admin/Users/Create", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["Input.UserName"] = name, ["Input.Role"] = "Candidate",
            ["Input.Password"] = "Integration-Only!482", ["Input.ConfirmPassword"] = "Integration-Only!482" }));
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, posted.Headers.Location!).AbsolutePath);
        Assert.Equal(before, await db.Users.CountAsync()); Assert.Null(await users.FindByNameAsync(name));
    }

    private static async Task<IdentityUser> CreateAdministratorAsync(UserManager<IdentityUser> users)
    {
        var user = new IdentityUser { UserName = "auth.admin." + Guid.NewGuid().ToString("N") };
        Assert.True((await users.CreateAsync(user, "Integration-Only!482")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Administrator")).Succeeded);
        return user;
    }
}
