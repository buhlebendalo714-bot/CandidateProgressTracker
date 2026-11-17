using System.Net;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class AccountLifecycleTests(WebAccessFixture fixture):IClassFixture<WebAccessFixture>
{
    private const string Password="Lifecycle-Only!482";
    private async Task<ReviewTestContext> CreateContextAsync()
    {
        // Each lifecycle test mutates its own legacy account. Never change the
        // class fixture's candidate role/state and contaminate another test.
        await using var scope=fixture.Services.CreateAsyncScope();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var candidate=new IdentityUser{UserName="lifecycle.owner."+Guid.NewGuid().ToString("N")};
        Assert.True((await users.CreateAsync(candidate,Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(candidate,"Candidate")).Succeeded);
        return await ReviewTestContext.CreateAsync(fixture,candidate.Id);
    }

    [Fact]
    public async Task Concurrent_redemption_uses_separate_contexts_and_commits_exactly_one_activation()
    {
        await using var t=await CreateContextAsync();var service=t.Services.GetRequiredService<IAccountLifecycleService>();
        var name="redeem."+Guid.NewGuid().ToString("N");var issued=await service.CreateAsync(name,name+"@example.com","Candidate",t.Seed.AdministratorId);Assert.True(issued.Succeeded);
        var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<AccountOperation> Redeem(){await using var scope=fixture.Services.CreateAsyncScope();var scoped=scope.ServiceProvider.GetRequiredService<IAccountLifecycleService>();await start.Task;return await scoped.RedeemAsync(name,issued.Credential!.Credential,Password);}
        var one=Redeem();var two=Redeem();start.SetResult();var results=await Task.WhenAll(one,two);Assert.Single(results,r=>r.Succeeded);
        Assert.Single(await t.Db.AccountChanges.Where(c=>c.Action=="Activation completed"&&c.UserId==(t.Db.Users.Where(u=>u.UserName==name).Select(u=>u.Id).First())).ToListAsync());
    }
    [Fact]
    public async Task Profile_and_role_update_revokes_cookie_without_rewriting_membership_history()
    {
        await using var t=await CreateContextAsync();var service=t.Services.GetRequiredService<IAccountLifecycleService>();var account=(await service.GetAsync(t.Seed.CandidateId,t.Seed.AdministratorId))!;
        var before=await t.Db.CohortMemberships.AsNoTracking().SingleAsync(m=>m.UserId==account.UserId&&m.CohortId==t.Seed.CohortId);
        using var client=await fixture.CreateClientAsync("Candidate",account.UserId);
        var result=await service.UpdateAsync(account.UserId,"role."+Guid.NewGuid().ToString("N")+"@example.com",["Mentor"],account.Version,"Correct role",t.Seed.AdministratorId);Assert.True(result.Succeeded,result.Error);
        var denied=await client.GetAsync("/Candidate");Assert.Equal(HttpStatusCode.Redirect,denied.StatusCode);Assert.Contains("/Account/Login",denied.Headers.Location!.OriginalString);
        var after=await t.Db.CohortMemberships.AsNoTracking().SingleAsync(m=>m.MembershipId==before.MembershipId);Assert.Equal(before.RowVersion,after.RowVersion);Assert.Null(after.EndsAt);
    }
    [Fact]
    public async Task Pending_activation_reissue_expiry_single_use_recovery_and_deactivation_preserve_identity_and_audit()
    {
        await using var t=await CreateContextAsync();var service=t.Services.GetRequiredService<IAccountLifecycleService>();var users=t.Services.GetRequiredService<UserManager<IdentityUser>>();
        var name="lifecycle."+Guid.NewGuid().ToString("N");var result=await service.CreateAsync(name,name+"@example.com","Candidate",t.Seed.AdministratorId);Assert.True(result.Succeeded,result.Error);
        var receipt=result.Credential!;Assert.InRange(receipt.ExpiresAt-DateTimeOffset.UtcNow,TimeSpan.FromHours(23.9),TimeSpan.FromHours(24));
        var user=(await users.FindByNameAsync(name))!;Assert.False(await AccountAvailability.IsActiveAsync(t.Db,user.Id));Assert.False(await users.HasPasswordAsync(user));
        var state=(await service.GetAsync(user.Id,t.Seed.AdministratorId))!;
        var second=await service.IssueAsync(user.Id,"Activation",state.Version,"Reissue",t.Seed.AdministratorId);Assert.True(second.Succeeded,second.Error);
        Assert.False((await service.RedeemAsync(name,receipt.Credential,Password)).Succeeded);
        Assert.True((await service.RedeemAsync(name,second.Credential!.Credential,Password)).Succeeded);
        Assert.False((await service.RedeemAsync(name,second.Credential.Credential,Password)).Succeeded);
        Assert.True(await AccountAvailability.IsActiveAsync(t.Db,user.Id));Assert.True(await users.CheckPasswordAsync(user,Password));
        state=(await service.GetAsync(user.Id,t.Seed.AdministratorId))!;var stamp=user.SecurityStamp;
        var recovery=await service.IssueAsync(user.Id,"Recovery",state.Version,"Lost password",t.Seed.AdministratorId);Assert.True(recovery.Succeeded,recovery.Error);Assert.InRange(recovery.Credential!.ExpiresAt-DateTimeOffset.UtcNow,TimeSpan.FromMinutes(59),TimeSpan.FromHours(1));Assert.NotEqual(stamp,user.SecurityStamp);
        var token=await t.Db.AccountCredentials.SingleAsync(c=>c.UserId==user.Id&&c.UsedAt==null&&c.RevokedAt==null);Assert.DoesNotContain(recovery.Credential.Credential,token.TokenHash);token.ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(-1);token.IssuedAt=DateTimeOffset.UtcNow.AddHours(-2);await t.Db.SaveChangesAsync();
        Assert.False((await service.RedeemAsync(name,recovery.Credential.Credential,Password)).Succeeded);
        state=(await service.GetAsync(user.Id,t.Seed.AdministratorId))!;Assert.True((await service.DeactivateAsync(user.Id,state.Version,"Leaving",t.Seed.AdministratorId)).Succeeded);
        Assert.False(await AccountAvailability.IsActiveAsync(t.Db,user.Id));state=(await service.GetAsync(user.Id,t.Seed.AdministratorId))!;
        Assert.False((await service.IssueAsync(user.Id,"Activation",state.Version," ",t.Seed.AdministratorId)).Succeeded);
        var activate=await service.IssueAsync(user.Id,"Activation",state.Version,"Return approved",t.Seed.AdministratorId);Assert.True(activate.Succeeded);Assert.True((await service.RedeemAsync(name,activate.Credential!.Credential,Password)).Succeeded);
        Assert.Empty(await t.Db.CohortMemberships.Where(m=>m.UserId==user.Id).ToListAsync());Assert.True(await t.Db.AccountChanges.CountAsync(c=>c.UserId==user.Id)>=7);
        Assert.DoesNotContain(await t.Db.AuditEvents.Where(a=>a.EntityId==user.Id).ToListAsync(),a=>a.Details.Contains(Password)||a.Details.Contains(receipt.Credential));
    }
    [Fact]
    public async Task Current_admin_unique_email_stale_forms_last_admin_and_candidate_deactivation_are_enforced_in_services()
    {
        await using var t=await CreateContextAsync();var service=t.Services.GetRequiredService<IAccountLifecycleService>();
        Assert.Null(await service.GetAsync(t.Seed.CandidateId,t.Seed.CandidateId));Assert.False((await service.CreateAsync("forged","forged@example.com","Candidate",t.Seed.CandidateId)).Succeeded);
        var suffix=Guid.NewGuid().ToString("N");var created=await service.CreateAsync("duplicate."+suffix,suffix+"@example.com","Mentor",t.Seed.AdministratorId);Assert.True(created.Succeeded);
        Assert.False((await service.CreateAsync("another."+suffix,suffix.ToUpperInvariant()+"@EXAMPLE.COM","Candidate",t.Seed.AdministratorId)).Succeeded);
        var admin=(await service.GetAsync(t.Seed.AdministratorId,t.Seed.AdministratorId))!;Assert.False((await service.DeactivateAsync(admin.UserId,admin.Version,"Cannot remove last admin",admin.UserId)).Succeeded);
        var candidate=(await service.GetAsync(t.Seed.CandidateId,t.Seed.AdministratorId))!;Assert.True((await service.DeactivateAsync(candidate.UserId,candidate.Version,"Access removed",admin.UserId)).Succeeded);
        Assert.False((await service.IssueAsync(candidate.UserId,"Activation",candidate.Version,"Stale",admin.UserId)).Succeeded);
        Assert.Null(await t.Services.GetRequiredService<CandidateProgressTracker.Application.Learning.IIndividualSubmissionQueryService>().GetHistoryAsync(t.Seed.AssignmentId,candidate.UserId));
        Assert.False((await t.Submissions.SubmitAsync(t.OriginalRequest,candidate.UserId)).Succeeded);
        Assert.Single(await t.Db.Submissions.Where(s=>s.AssignmentId==t.Seed.AssignmentId).ToListAsync());
    }
    [Fact]
    public async Task Existing_cookie_is_revoked_on_next_request_and_credential_POST_requires_antiforgery()
    {
        await using var t=await CreateContextAsync();var service=t.Services.GetRequiredService<IAccountLifecycleService>();
        using var candidate=await fixture.CreateClientAsync("Candidate",t.Seed.CandidateId);Assert.Equal(HttpStatusCode.OK,(await candidate.GetAsync("/Candidate")).StatusCode);
        var account=(await service.GetAsync(t.Seed.CandidateId,t.Seed.AdministratorId))!;Assert.True((await service.IssueAsync(account.UserId,"Recovery",account.Version,"Recovery",t.Seed.AdministratorId)).Succeeded);
        var response=await candidate.GetAsync("/Candidate");Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Contains("/Account/Login",response.Headers.Location!.OriginalString);
        using var anonymous=await fixture.CreateClientAsync(null);using var forged=await anonymous.PostAsync("/Account/CompleteCredential",new FormUrlEncodedContent(new Dictionary<string,string>{{"UserName","guess"},{"Credential","guess"},{"Password",Password}}));Assert.Equal(HttpStatusCode.BadRequest,forged.StatusCode);
    }
}
