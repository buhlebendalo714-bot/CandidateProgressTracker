using System.Net;
using System.Data.Common;
using CandidateProgressTracker.Infrastructure.Cohorts;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text;
using System.Text.Json;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;
public sealed class BulkOnboardingImportTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    private static async Task<OnboardingConfirmation> Draft(IServiceProvider services,Guid cohort,string actor,string csv)
    {
        var bytes=Encoding.UTF8.GetBytes(csv);var date=DateOnly.FromDateTime(DateTime.UtcNow);
        var preview=await services.GetRequiredService<IBulkOnboardingPreviewService>().PreviewAsync(cohort,date,bytes,actor);
        Assert.NotNull(preview);Assert.Empty(preview.Issues);Assert.DoesNotContain(preview.Rows,r=>r.Action==OnboardingAction.Conflict);
        return new(Guid.NewGuid(),cohort,date,bytes,OnboardingFingerprint.Of(preview),actor);
    }
    [Fact]
    public async Task Concurrent_confirmation_and_replay_create_one_batch_and_preserve_existing_account()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var s=scope.ServiceProvider;var db=s.GetRequiredService<TrackerDbContext>();
        var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);var users=s.GetRequiredService<UserManager<IdentityUser>>();
        var existing=new IdentityUser("bulk.existing."+Guid.NewGuid().ToString("N")){Email=Guid.NewGuid().ToString("N")+"@example.com"};
        Assert.True((await users.CreateAsync(existing,"Integration-Only!482")).Succeeded);Assert.True((await users.AddToRoleAsync(existing,"Candidate")).Succeeded);
        var preserved=JsonSerializer.Serialize(new{existing.Email,existing.NormalizedEmail,existing.PasswordHash,existing.SecurityStamp,existing.ConcurrencyStamp});
        var credentialCount=await db.AccountCredentials.CountAsync();
        var name="bulk.new."+Guid.NewGuid().ToString("N");var enrolled=(await users.FindByIdAsync(seed.CandidateId))!;
        var request=await Draft(s,seed.CohortId,seed.AdministratorId,$"UserName,Email\n{name},{name}@example.com\n{existing.UserName},{existing.Email}\n{enrolled.UserName},\n");
        async Task<OnboardingImportResult?> Confirm()
        {await using var call=fixture.Services.CreateAsyncScope();return await call.ServiceProvider.GetRequiredService<IBulkOnboardingImportService>().ConfirmAsync(request,seed.AdministratorId);}
        var calls=await Task.WhenAll(Confirm(),Confirm());Assert.All(calls,r=>{Assert.NotNull(r);Assert.True(r.Complete);});
        db.ChangeTracker.Clear();Assert.Single(await db.OnboardingBatches.Where(b=>b.BatchId==request.RequestId).ToListAsync());
        var rows=await db.OnboardingBatchRows.Where(r=>r.BatchId==request.RequestId).OrderBy(r=>r.Row).ToListAsync();
        Assert.Equal(new[]{"Created","Enrolled","Skipped"},rows.Select(r=>r.Status));
        var created=await db.Users.SingleAsync(u=>u.UserName==name);Assert.Null(created.PasswordHash);
        Assert.False((await db.AccountAccessStates.SingleAsync(a=>a.UserId==created.Id)).Enabled);
        Assert.Equal(credentialCount,await db.AccountCredentials.CountAsync());
        var membership=await db.CohortMemberships.SingleAsync(m=>m.MembershipId==rows[0].MembershipId);
        Assert.Equal(new DateTimeOffset(request.StartDate.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(2)),membership.StartsAt);
        var unchanged=await db.Users.SingleAsync(u=>u.Id==existing.Id);
        Assert.Equal(preserved,JsonSerializer.Serialize(new{unchanged.Email,unchanged.NormalizedEmail,unchanged.PasswordHash,unchanged.SecurityStamp,unchanged.ConcurrencyStamp}));
        Assert.Equal(new[]{"Candidate"},await users.GetRolesAsync(unchanged));
        var versions=rows.Select(r=>Convert.ToBase64String(r.RowVersion)).ToArray();
        var imports=s.GetRequiredService<IBulkOnboardingImportService>();Assert.True((await imports.ConfirmAsync(request,seed.AdministratorId))!.Complete);
        Assert.Equal(versions,await db.OnboardingBatchRows.AsNoTracking().Where(r=>r.BatchId==request.RequestId).OrderBy(r=>r.Row).Select(r=>Convert.ToBase64String(r.RowVersion)).ToArrayAsync());
        var changed=await imports.ConfirmAsync(request with {Csv=Encoding.UTF8.GetBytes("UserName\nchanged")},seed.AdministratorId);
        Assert.NotNull(changed);Assert.Empty(changed.Rows);Assert.False(changed.Complete);
        Assert.Null(await imports.GetAsync(request.RequestId,seed.CandidateId));
        var audit=await db.AuditEvents.AsNoTracking().Where(a=>a.ActorId==seed.AdministratorId).ToListAsync();
        Assert.Contains(audit,a=>a.EntityType=="OnboardingBatch"&&a.EntityId==request.RequestId.ToString()&&a.Action=="Added");
        Assert.Contains(audit,a=>a.EntityType=="OnboardingBatchRow"&&a.EntityId.StartsWith(request.RequestId.ToString()+"/")&&a.Action=="Modified");
        Assert.Contains(audit,a=>a.EntityType=="IdentityUser"&&a.EntityId==created.Id&&a.Action=="Added");
        Assert.Contains(audit,a=>a.EntityType=="CohortMembership"&&a.EntityId==membership.MembershipId.ToString()&&a.Action=="Added");
        var filtered=await s.GetRequiredService<CandidateProgressTracker.Application.Accounts.IAuditQueryService>()
            .SearchAsync(new(seed.AdministratorId,"Added",request.RequestId.ToString(),null,null,1),seed.AdministratorId);
        Assert.NotNull(filtered);
        Assert.Contains(filtered.Rows,a=>a.EntityType=="OnboardingBatch"&&a.EntityId==request.RequestId.ToString());
    }

    [Fact]
    public async Task Failed_membership_rolls_back_new_identity_and_retry_resumes_only_unfinished_rows()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var s=scope.ServiceProvider;var db=s.GetRequiredService<TrackerDbContext>();
        Assert.StartsWith("CandidateProgressTracker_Tests_",db.Database.GetDbConnection().Database);
        var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);var suffix=Guid.NewGuid().ToString("N");
        var first="atomic.first."+suffix;var fail="atomic.fail."+suffix;var last="atomic.last."+suffix;
        var request=await Draft(s,seed.CohortId,seed.AdministratorId,$"UserName,Email\n{first},{first}@example.com\n{fail},{fail}@example.com\n{last},{last}@example.com\n");
        var fault=new FailSecondMembership();
        var services=new ServiceCollection();services.AddLogging();
        services.AddDbContext<TrackerDbContext>(o=>o.UseSqlServer(db.Database.GetConnectionString()).AddInterceptors(fault));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<TrackerDbContext>();
        services.AddScoped<IBulkOnboardingPreviewService,BulkOnboardingPreviewService>();
        services.AddScoped<IBulkOnboardingImportService,BulkOnboardingImportService>();
        await using var provider=services.BuildServiceProvider();
        await using var failingScope=provider.CreateAsyncScope();
        var imports=failingScope.ServiceProvider.GetRequiredService<IBulkOnboardingImportService>();
            var partial=await imports.ConfirmAsync(request,seed.AdministratorId);Assert.NotNull(partial);Assert.False(partial.Complete);
            Assert.Equal(new[]{"Created","Failed","Pending"},partial.Rows.Select(r=>r.Status));
            Assert.False(await db.Users.AnyAsync(u=>u.UserName==fail||u.UserName==last));
            Assert.Equal(1,await db.OnboardingBatchRows.CountAsync(r=>r.BatchId==request.RequestId&&r.MembershipId!=null));
            var firstRow=await db.OnboardingBatchRows.AsNoTracking().SingleAsync(r=>r.BatchId==request.RequestId&&r.Row==2);
            fault.Enabled=false;
            var users=s.GetRequiredService<UserManager<IdentityUser>>();var admin=(await users.FindByIdAsync(seed.AdministratorId))!;
            Assert.True((await users.RemoveFromRoleAsync(admin,"Administrator")).Succeeded);
            Assert.Null(await imports.ResumeAsync(request.RequestId,seed.AdministratorId));
            Assert.Null(await imports.GetAsync(request.RequestId,seed.AdministratorId));
            Assert.False(await db.Users.AnyAsync(u=>u.UserName==fail||u.UserName==last));
            Assert.True((await users.AddToRoleAsync(admin,"Administrator")).Succeeded);
            var cohort=await db.Cohorts.SingleAsync(c=>c.CohortId==seed.CohortId);cohort.IsArchived=true;await db.SaveChangesAsync();
            var paused=await imports.ResumeAsync(request.RequestId,seed.AdministratorId);Assert.NotNull(paused);Assert.False(paused.Complete);
            Assert.False(await db.Users.AnyAsync(u=>u.UserName==fail||u.UserName==last));
            cohort.IsArchived=false;await db.SaveChangesAsync();
            await using var restarted=provider.CreateAsyncScope();
            var completed=await restarted.ServiceProvider.GetRequiredService<IBulkOnboardingImportService>().ResumeAsync(request.RequestId,seed.AdministratorId);Assert.NotNull(completed);Assert.True(completed.Complete);
            Assert.All(completed.Rows,r=>Assert.Equal("Created",r.Status));
            var after=await db.OnboardingBatchRows.AsNoTracking().SingleAsync(r=>r.BatchId==request.RequestId&&r.Row==2);
            Assert.Equal(firstRow.UserId,after.UserId);Assert.Equal(firstRow.MembershipId,after.MembershipId);Assert.Equal(firstRow.RowVersion,after.RowVersion);
            Assert.Equal(3,await db.Users.CountAsync(u=>u.UserName==first||u.UserName==fail||u.UserName==last));
            Assert.Equal(3,await db.OnboardingBatchRows.CountAsync(r=>r.BatchId==request.RequestId&&r.MembershipId!=null));
    }

    private sealed class FailSecondMembership : DbCommandInterceptor
    {
        private int count; public bool Enabled { get; set; }=true;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,
            InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
        {
            if(Enabled&&command.CommandText.Contains("INSERT INTO [CohortMemberships]",StringComparison.Ordinal)&&Interlocked.Increment(ref count)==2)
                throw new DbUpdateException("Injected membership save failure after Identity writes");
            return base.ReaderExecutingAsync(command,eventData,result,cancellationToken);
        }
    }

    [Fact]
    public async Task Stale_preview_archived_cohort_and_revoked_administrator_cannot_import()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var s=scope.ServiceProvider;var db=s.GetRequiredService<TrackerDbContext>();
        var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);var users=s.GetRequiredService<UserManager<IdentityUser>>();
        var name="stale."+Guid.NewGuid().ToString("N");var request=await Draft(s,seed.CohortId,seed.AdministratorId,$"UserName,Email\n{name},{name}@example.com\n");
        var appeared=new IdentityUser(name){Email=name+"@example.com"};Assert.True((await users.CreateAsync(appeared)).Succeeded);Assert.True((await users.AddToRoleAsync(appeared,"Candidate")).Succeeded);
        var imports=s.GetRequiredService<IBulkOnboardingImportService>();var stale=await imports.ConfirmAsync(request,seed.AdministratorId);
        Assert.NotNull(stale);Assert.Empty(stale.Rows);Assert.False(await db.OnboardingBatches.AnyAsync(b=>b.BatchId==request.RequestId));
        var cohort=await db.Cohorts.SingleAsync(c=>c.CohortId==seed.CohortId);cohort.IsArchived=true;await db.SaveChangesAsync();
        Assert.Null(await imports.ConfirmAsync(request,seed.AdministratorId));
        var admin=new IdentityUser("revoked.import."+Guid.NewGuid().ToString("N"));Assert.True((await users.CreateAsync(admin)).Succeeded);Assert.True((await users.AddToRoleAsync(admin,"Administrator")).Succeeded);
        Assert.True((await users.RemoveFromRoleAsync(admin,"Administrator")).Succeeded);
        Assert.Null(await imports.ConfirmAsync(request with {Actor=admin.Id},admin.Id));
        Assert.False(await db.CohortMemberships.AnyAsync(m=>m.UserId==appeared.Id));
    }

    [Fact]
    public async Task Http_protected_preview_confirm_and_resume_preserve_antiforgery_and_ticket_binding()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var s=scope.ServiceProvider;var db=s.GetRequiredService<TrackerDbContext>();
        var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);using var admin=await fixture.CreateClientAsync("Administrator",seed.AdministratorId);
        var route="/Admin/Cohorts/BulkOnboard/"+seed.CohortId;var get=await admin.GetAsync(route);SubmissionPageTests.CopyResponseCookies(admin,get);
        var html=await get.Content.ReadAsStringAsync();var token=SubmissionPageTests.Hidden(html,"__RequestVerificationToken");
        var name="http.import."+Guid.NewGuid().ToString("N");
        using var upload=new MultipartFormDataContent{{new StringContent(token),"__RequestVerificationToken"},{new StringContent(DateTime.UtcNow.ToString("yyyy-MM-dd")),"StartDate"},{new ByteArrayContent(Encoding.UTF8.GetBytes($"UserName,Email\n{name},{name}@example.com\n")),"CsvFile","candidates.csv"}};
        var preview=await admin.PostAsync(route,upload);Assert.Equal(HttpStatusCode.OK,preview.StatusCode);html=await preview.Content.ReadAsStringAsync();
        Assert.False(await db.Users.AnyAsync(u=>u.UserName==name));var ticket=SubmissionPageTests.Hidden(html,"ConfirmationTicket");
        Assert.False(await db.OnboardingBatches.AnyAsync(b=>b.CohortId==seed.CohortId));
        var confirm=await admin.PostAsync(route+"?handler=Confirm",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",token},{"ConfirmationTicket",ticket}}));
        Assert.Equal(HttpStatusCode.Redirect,confirm.StatusCode);var location=confirm.Headers.Location!.ToString();Assert.Contains("OnboardingBatch",location);
        var batch=await admin.GetAsync(location);Assert.Equal(HttpStatusCode.OK,batch.StatusCode);Assert.Contains("Onboarding complete.",await batch.Content.ReadAsStringAsync());
        var resultHtml=await batch.Content.ReadAsStringAsync();
        var createdId=await db.Users.Where(u=>u.UserName==name).Select(u=>u.Id).SingleAsync();
        Assert.Contains("/Admin/Users/Manage?UserId="+createdId,resultHtml);
        Assert.Contains("Manage account / issue activation",resultHtml);
        var credentialCount=await db.AccountCredentials.CountAsync();var auditCount=await db.AuditEvents.CountAsync();
        var export=await admin.GetAsync(location+"?handler=Export");Assert.Equal(HttpStatusCode.OK,export.StatusCode);
        Assert.Equal("text/csv",export.Content.Headers.ContentType!.MediaType);
        Assert.True(export.Headers.CacheControl!.NoStore);
        Assert.Equal("attachment",export.Content.Headers.ContentDisposition!.DispositionType);
        var csv=await export.Content.ReadAsStringAsync();
        Assert.Equal(new byte[]{239,187,191},(await export.Content.ReadAsByteArrayAsync()).Take(3).ToArray());
        Assert.StartsWith("CSV row,UserName,Status,Details\r\n",csv.TrimStart('\uFEFF'));
        Assert.Contains("\""+name+"\",\"Created\"",csv);
        Assert.DoesNotContain("Password",csv,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(credentialCount,await db.AccountCredentials.CountAsync());Assert.Equal(auditCount,await db.AuditEvents.CountAsync());
        using var candidate=await fixture.CreateClientAsync("Candidate",seed.CandidateId);
        Assert.NotEqual(HttpStatusCode.OK,(await candidate.GetAsync(location+"?handler=Export")).StatusCode);
        var other=new IdentityUser("export.admin."+Guid.NewGuid().ToString("N"));
        var users=s.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.CreateAsync(other)).Succeeded);Assert.True((await users.AddToRoleAsync(other,"Administrator")).Succeeded);
        using var otherAdmin=await fixture.CreateClientAsync("Administrator",other.Id);
        Assert.Equal(HttpStatusCode.NotFound,(await otherAdmin.GetAsync(location+"?handler=Export")).StatusCode);
        var replay=await admin.PostAsync(route+"?handler=Confirm",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",token},{"ConfirmationTicket",ticket}}));Assert.Equal(HttpStatusCode.Redirect,replay.StatusCode);
        Assert.Single(await db.Users.Where(u=>u.UserName==name).ToListAsync());
        var saved=await admin.GetStringAsync(route);Assert.Contains("Your saved batches",saved);Assert.Contains(location,saved);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsync(location,new FormUrlEncodedContent([]))).StatusCode);
        var corrupt=await admin.PostAsync(route+"?handler=Confirm",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",token},{"ConfirmationTicket","invalid"}}));
        Assert.Equal(HttpStatusCode.OK,corrupt.StatusCode);Assert.Contains("preview expired or is invalid",await corrupt.Content.ReadAsStringAsync());
    }
}
