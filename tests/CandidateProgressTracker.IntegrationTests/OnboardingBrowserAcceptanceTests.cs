using System.Security.Cryptography;
using System.Text.Json;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class OnboardingBrowserFactAttribute : FactAttribute
{
    public OnboardingBrowserFactAttribute()
    {
        if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("CPT_ONBOARDING_BROWSER")!="1")
            Skip="Opt-in live Chrome onboarding acceptance with disposable data.";
    }
}
public sealed class OnboardingBrowserAcceptanceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [OnboardingBrowserFact]
    public async Task Live_forms_preview_confirm_results_and_activation_handoff()
    {
        var password="Accept!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(18))+"a9";
        var root=Path.Combine(Path.GetTempPath(),"CptOnboarding_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var scope=fixture.Services.CreateAsyncScope();var services=scope.ServiceProvider;
            var db=services.GetRequiredService<TrackerDbContext>();
            Assert.StartsWith("CandidateProgressTracker_Tests_",db.Database.GetDbConnection().Database);
            var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);
            var users=services.GetRequiredService<UserManager<IdentityUser>>();
            var admin=(await users.FindByIdAsync(seed.AdministratorId))!;
            Assert.True((await users.RemovePasswordAsync(admin)).Succeeded);Assert.True((await users.AddPasswordAsync(admin,password)).Succeeded);
            var existing=new IdentityUser("browser.enrol."+Guid.NewGuid().ToString("N")){Email=Guid.NewGuid().ToString("N")+"@example.com"};
            Assert.True((await users.CreateAsync(existing)).Succeeded);Assert.True((await users.AddToRoleAsync(existing,"Candidate")).Succeeded);
            var enrolled=(await users.FindByIdAsync(seed.CandidateId))!;
            var newName="browser.create."+Guid.NewGuid().ToString("N");
            var csvFile=Path.Combine(root,"valid.csv");var duplicateFile=Path.Combine(root,"duplicate.csv");
            await File.WriteAllTextAsync(csvFile,$"UserName,Email\r\n{newName},{newName}@example.com\r\n{existing.UserName},{existing.Email}\r\n{enrolled.UserName},\r\n");
            await File.WriteAllTextAsync(duplicateFile,$"UserName,Email\r\n{newName},{newName}@example.com\r\n{newName},{newName}@example.com\r\n");
            var credentials=await db.AccountCredentials.CountAsync();
            var repository=new DirectoryInfo(AppContext.BaseDirectory);
            while(repository is not null&&!File.Exists(Path.Combine(repository.FullName,"AGENTS.md")))repository=repository.Parent;
            Assert.NotNull(repository);
            var errors=new System.Collections.Concurrent.ConcurrentQueue<string>();
            await using var factory=new AccessWebFactory(db.Database.GetConnectionString()!);
            await using var live=factory.WithWebHostBuilder(b=>b
                .ConfigureLogging(l=>l.AddProvider(new AccountLifecycleBrowserAcceptanceTests.SafeErrorTypes(errors)))
                .ConfigureServices(s=>s.AddSingleton<IStartupFilter>(new AssetTimings(errors))));
            live.UseKestrel(o=>o.ListenLocalhost(7265,l=>l.UseHttps()));live.StartServer();
            using(var probe=new HttpClient(new HttpClientHandler {ServerCertificateCustomValidationCallback=HttpClientHandler.DangerousAcceptAnyServerCertificateValidator}))
            {
                foreach(var asset in new[]{"/lib/bootstrap/dist/css/bootstrap.min.46ein0sx1k.css","/lib/jquery/dist/jquery.min.o1o13a6vjx.js"})
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    using var response=await probe.GetAsync("https://localhost:7265"+asset);
                    errors.Enqueue($"ANONYMOUS ASSET {asset}: {(int)response.StatusCode}; {timer.ElapsedMilliseconds}ms");
                    Assert.True(response.IsSuccessStatusCode,"Isolated anonymous asset failed");
                }
            }
            var start=new System.Diagnostics.ProcessStartInfo("node"){WorkingDirectory=repository.FullName,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("tests/CandidateProgressTracker.IntegrationTests/Browser/course-files.cjs");
            start.Environment["CPT_COURSE_CONFIG"]=JsonSerializer.Serialize(new{journey="onboarding",password,adminName=admin.UserName,cohortId=seed.CohortId,startDate=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-dd"),csvFile,duplicateFile});
            using var process=System.Diagnostics.Process.Start(start)!;
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));
            try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);throw;}
            var diagnostics=(await stdout)+Environment.NewLine+(await stderr)+Environment.NewLine+string.Join(Environment.NewLine,errors);
            var output=Path.Combine(repository.FullName,"TestResults","onboarding-browser");Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output,"browser-output.txt"),diagnostics);
            Assert.True(process.ExitCode==0,diagnostics);
            db.ChangeTracker.Clear();
            var batch=await db.OnboardingBatches.SingleAsync(b=>b.CohortId==seed.CohortId);
            Assert.Equal(new[]{"Created","Enrolled","Skipped"},await db.OnboardingBatchRows.Where(r=>r.BatchId==batch.BatchId).OrderBy(r=>r.Row).Select(r=>r.Status).ToArrayAsync());
            var created=await db.Users.SingleAsync(u=>u.UserName==newName);Assert.Null(created.PasswordHash);
            Assert.False((await db.AccountAccessStates.SingleAsync(s=>s.UserId==created.Id)).Enabled);
            Assert.Equal(credentials,await db.AccountCredentials.CountAsync());
            Assert.Single(await db.CohortMemberships.Where(m=>m.CohortId==seed.CohortId&&m.UserId==created.Id).ToListAsync());
        }
        finally
        {
            var resolved=Path.GetFullPath(root);
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),Path.GetDirectoryName(resolved));
            Assert.True(Guid.TryParseExact(Path.GetFileName(resolved)["CptOnboarding_".Length..],"N",out _));
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
    private sealed class AssetTimings(System.Collections.Concurrent.ConcurrentQueue<string> output) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>
        {
            app.Use(async(context,continuation)=>
            {
                var path=context.Request.Path.Value??"";
                if(!(path.EndsWith(".js")||path.EndsWith(".css")||path.EndsWith(".webp"))){await continuation();return;}
                var timer=System.Diagnostics.Stopwatch.StartNew();
                output.Enqueue($"ASSET START {path}");
                try{await continuation();}
                finally{output.Enqueue($"ASSET END {path}: {context.Response.StatusCode}; {timer.ElapsedMilliseconds}ms; aborted={context.RequestAborted.IsCancellationRequested}");}
            });
            next(app);
        };
    }
}
