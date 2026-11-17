using System.Net;
using System.Text;
using System.Text.Json;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class BulkOnboardingPreviewTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    private static byte[] Csv(string value) => Encoding.UTF8.GetBytes(value);
    private static async Task<string> Snapshot(TrackerDbContext db) => JsonSerializer.Serialize(new {
        Users = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new {u.Id,u.UserName,u.NormalizedUserName,u.Email,u.NormalizedEmail,u.PasswordHash,u.SecurityStamp,u.ConcurrencyStamp}).ToListAsync(),
        Roles = await db.UserRoles.AsNoTracking().OrderBy(r => r.UserId).ThenBy(r => r.RoleId).ToListAsync(),
        Members = await db.CohortMemberships.AsNoTracking().OrderBy(m => m.MembershipId).ToListAsync(),
        States = await db.AccountAccessStates.CountAsync(), Credentials = await db.AccountCredentials.CountAsync(),
        Audit = await db.AuditEvents.CountAsync(), Changes = await db.AccountChanges.CountAsync()
    });

    [Fact]
    public async Task Preview_classifies_actions_and_conflicts_without_mutating_any_accounts_or_memberships()
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var s = scope.ServiceProvider;
        var db = s.GetRequiredService<TrackerDbContext>(); var users = s.GetRequiredService<UserManager<IdentityUser>>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        async Task<IdentityUser> Candidate(string label)
        {
            var user = new IdentityUser(label + Guid.NewGuid().ToString("N")) { Email = Guid.NewGuid().ToString("N") + "@example.com" };
            Assert.True((await users.CreateAsync(user)).Succeeded); Assert.True((await users.AddToRoleAsync(user,"Candidate")).Succeeded); return user;
        }
        var existing = await Candidate("existing."); var ended = await Candidate("ended."); var future = await Candidate("future."); var inactive = await Candidate("inactive.");
        db.CohortMemberships.AddRange(
            new() { CohortId=seed.CohortId,UserId=ended.Id,MembershipKind=MembershipKind.Candidate,StartsAt=DateTimeOffset.UtcNow.AddDays(-2),EndsAt=DateTimeOffset.UtcNow.AddDays(-1),CreatedBy=seed.AdministratorId,ChangeReason="Preview fixture" },
            new() { CohortId=seed.CohortId,UserId=future.Id,MembershipKind=MembershipKind.Candidate,StartsAt=DateTimeOffset.UtcNow.AddDays(2),CreatedBy=seed.AdministratorId,ChangeReason="Preview fixture" });
        db.AccountAccessStates.Add(new() { UserId=inactive.Id,Enabled=false,ChangedBy=seed.AdministratorId,ChangedAt=DateTimeOffset.UtcNow,Reason="Preview fixture" });
        await db.SaveChangesAsync();
        var enrolledMember = await db.CohortMemberships.SingleAsync(m=>m.CohortId==seed.CohortId && m.UserId==seed.CandidateId);
        enrolledMember.EndsAt=DateTimeOffset.UtcNow.AddDays(7); await db.SaveChangesAsync();
        var enrolled = (await users.FindByIdAsync(seed.CandidateId))!.UserName!;
        var roleConflict = (await users.FindByIdAsync(seed.AdministratorId))!.UserName!;
        var revoked = await Candidate("revoked."); Assert.True((await users.AddToRoleAsync(revoked,"Administrator")).Succeeded);
        var before = await Snapshot(db); var service = s.GetRequiredService<IBulkOnboardingPreviewService>();
        var name = "new." + Guid.NewGuid().ToString("N");
        var result = await service.PreviewAsync(seed.CohortId,new(2026,11,16),Csv($"UserName,Email\r\n{name},{name}@example.com\r\n{existing.UserName},{existing.Email}\r\n{enrolled},\r\n{ended.UserName},\r\n{future.UserName},\r\n{inactive.UserName},\r\n{roleConflict},\r\nduplicate,\r\nDUPLICATE,\r\n"),seed.AdministratorId);
        Assert.NotNull(result); Assert.Empty(result.Issues); Assert.Equal(9,result.Rows.Count);
        Assert.Equal(OnboardingAction.Create,result.Rows[0].Action);
        Assert.Equal(OnboardingAction.EnrolExisting,result.Rows[1].Action); Assert.Equal(existing.UserName,result.Rows[1].MatchedUserName);
        Assert.Equal(OnboardingAction.Skip,result.Rows[2].Action);
        Assert.All(result.Rows.Skip(3),r=>{Assert.Equal(OnboardingAction.Conflict,r.Action);Assert.NotEmpty(r.Issues);});
        Assert.All(result.Rows.Skip(7),r=>Assert.Contains(r.Issues,e=>e.Field=="UserName"&&e.Message.Contains("Duplicate")));
        Assert.Equal(before,await Snapshot(db));
        Assert.Null(await service.PreviewAsync(seed.CohortId,new(2026,11,16),Csv("UserName\nsecret"),seed.CandidateId));
        Assert.True((await users.RemoveFromRoleAsync(revoked,"Administrator")).Succeeded);
        Assert.Null(await service.PreviewAsync(seed.CohortId,new(2026,11,16),Csv("UserName\nsecret"),revoked.Id));
    }

    [Fact]
    public async Task Csv_validation_is_bounded_strict_utf8_and_uses_identity_email_and_username_rules()
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var s = scope.ServiceProvider; var db=s.GetRequiredService<TrackerDbContext>();
        var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);var service=s.GetRequiredService<IBulkOnboardingPreviewService>();
        async Task<BulkOnboardingPreview> Preview(byte[] csv) => (await service.PreviewAsync(seed.CohortId,new(2026,11,16),csv,seed.AdministratorId))!;
        var before=await Snapshot(db);
        Assert.NotEmpty((await Preview([0xff,0xfe])).Issues);
        Assert.NotEmpty((await Preview(new byte[IBulkOnboardingPreviewService.MaximumBytes+1])).Issues);
        Assert.NotEmpty((await Preview(Csv("UserName,Password\na,b"))).Issues);
        Assert.NotEmpty((await Preview(Csv("UserName,UserName\na,b"))).Issues);
        Assert.NotEmpty((await Preview(Csv("UserName,Email\n\"unclosed,a@example.com"))).Issues);
        Assert.NotEmpty((await Preview(Csv("UserName\n"+string.Join("\n",Enumerable.Range(0,101).Select(i=>"candidate"+i))))).Issues);
        var invalid=await Preview(Csv("UserName,Email\n=FORMULA,invalid\nmissing.email,\n"));
        Assert.All(invalid.Rows,r=>Assert.Equal(OnboardingAction.Conflict,r.Action));
        var validName="quoted."+Guid.NewGuid().ToString("N");
        var quoted=await Preview(Csv($"\uFEFFUserName,Email\r\n\r\n\"{validName}\",\"{validName}@example.com\"\r\n"));
        Assert.Equal(OnboardingAction.Create,Assert.Single(quoted.Rows).Action);Assert.Equal(3,quoted.Rows[0].Row);
        var multiline=await Preview(Csv("UserName,Email\n\"bad\nname\",bad@example.com\n"));
        Assert.Equal(2,Assert.Single(multiline.Rows).Row);Assert.Equal(OnboardingAction.Conflict,multiline.Rows[0].Action);
        Assert.Equal(before,await Snapshot(db));
        var cohort=await db.Cohorts.SingleAsync(c=>c.CohortId==seed.CohortId);cohort.IsArchived=true;await db.SaveChangesAsync();
        Assert.Null(await service.PreviewAsync(seed.CohortId,new(2026,11,16),Csv("UserName\na"),seed.AdministratorId));
    }

    [Fact]
    public async Task Administrator_template_and_antiforgery_upload_preview_are_read_only_and_other_roles_are_denied()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);var route="/Admin/Cohorts/BulkOnboard/"+seed.CohortId;
        using var admin=await fixture.CreateClientAsync("Administrator",seed.AdministratorId);
        var template=await admin.GetAsync(route+"?handler=Template");Assert.Equal(HttpStatusCode.OK,template.StatusCode);
        Assert.Contains("no-store",template.Headers.CacheControl!.ToString());
        Assert.Equal("UserName,Email\r\n",Encoding.UTF8.GetString(await template.Content.ReadAsByteArrayAsync()).TrimStart('\uFEFF'));
        var response=await admin.GetAsync(route);Assert.Equal(HttpStatusCode.OK,response.StatusCode);SubmissionPageTests.CopyResponseCookies(admin,response);
        var html=await response.Content.ReadAsStringAsync();var token=SubmissionPageTests.Hidden(html,"__RequestVerificationToken");
        var before=await Snapshot(db);
        using var form=new MultipartFormDataContent { { new StringContent(token),"__RequestVerificationToken" },{new StringContent("2026-11-16"),"StartDate"},
            {new ByteArrayContent(Csv("UserName,Email\npreview.http,preview.http@example.com\n")),"CsvFile","candidates.csv"} };
        var preview=await admin.PostAsync(route,form);Assert.Equal(HttpStatusCode.OK,preview.StatusCode);html=await preview.Content.ReadAsStringAsync();
        Assert.Contains("Create: 1",html);Assert.Contains("Confirm onboarding",html);Assert.Contains("ConfirmationTicket",html);Assert.Contains("preview.http",html);
        Assert.Equal(before,await Snapshot(db));
        using var candidate=await fixture.CreateClientAsync("Candidate",seed.CandidateId);
        Assert.NotEqual(HttpStatusCode.OK,(await candidate.GetAsync(route)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK,(await candidate.GetAsync(route+"?handler=Template")).StatusCode);
        using var noToken=new MultipartFormDataContent {{new StringContent("2026-11-16"),"StartDate"}};
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsync(route,noToken)).StatusCode);
        Assert.Equal(before,await Snapshot(db));
    }
}
