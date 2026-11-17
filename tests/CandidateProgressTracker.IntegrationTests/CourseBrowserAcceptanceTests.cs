using System.Security.Cryptography;
using System.Text.Json;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class CourseBrowserFactAttribute : FactAttribute
{
    public CourseBrowserFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CPT_COURSE_BROWSER") != "1")
            Skip = "Opt-in installed-Chrome course acceptance with real Defender and disposable data.";
    }
}

public sealed class CourseBrowserAcceptanceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [CourseBrowserFact]
    public async Task Course_certificate_browser_journey_uses_real_scanning_and_private_downloads()
    {
        var password = "Accept!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18)) + "a9";
        var control = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var storageRoot = Path.Combine(Path.GetTempPath(), "CptCourseBrowser_" + Guid.NewGuid().ToString("N"));
        string connection, adminName, candidateName, mentorName, outsiderName, candidateId;
        Guid cycleId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            Assert.StartsWith("CandidateProgressTracker_Tests_", db.Database.GetDbConnection().Database);
            connection = db.Database.GetConnectionString()!;
            var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
            cycleId = await db.Assignments.Where(a => a.AssignmentId == seed.AssignmentId).Select(a => a.CycleId).SingleAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = await users.FindByIdAsync(seed.AdministratorId);
            var candidate = await users.FindByIdAsync(seed.CandidateId);
            var mentor = await db.Users.SingleAsync(u => u.UserName!.StartsWith("web.Mentor."));
            var outsider = new IdentityUser("course.outsider." + Guid.NewGuid().ToString("N"));
            Assert.True((await users.CreateAsync(outsider, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(outsider, "Candidate")).Succeeded);
            foreach (var user in new[] { admin!, candidate!, mentor })
            {
                Assert.True((await users.RemovePasswordAsync(user)).Succeeded);
                Assert.True((await users.AddPasswordAsync(user, password)).Succeeded);
            }
            adminName = admin!.UserName!; candidateName = candidate!.UserName!; candidateId = candidate.Id;
            mentorName = mentor.UserName!; outsiderName = outsider.UserName!;
            db.CohortMemberships.Add(new CohortMembership { CohortId = seed.CohortId, UserId = mentor.Id,
                MembershipKind = MembershipKind.Mentor, StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedBy = admin.Id, ChangeReason = "Disposable course browser acceptance" });
            await db.SaveChangesAsync();
        }
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "AGENTS.md"))) repository = repository.Parent;
        Assert.NotNull(repository);
        await using var factory = new AccessWebFactory(connection);
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        await using var live = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(logging => logging.AddProvider(new AccountLifecycleBrowserAcceptanceTests.SafeErrorTypes(errors)))
            .ConfigureServices(services =>
        {
            services.Configure<EvidenceOptions>(o => { o.Root = storageRoot; o.RepositoryRoot = repository!.FullName; });
            // Testing normally disables background processing. This authenticated test-only control
            // drives the actual durable service and real Defender, after the browser sees Processing.
            services.AddSingleton<IStartupFilter>(new ProcessingControl(control));
        }));
        try
        {
            live.UseKestrel(o => o.ListenLocalhost(7265, l => l.UseHttps()));
            live.StartServer();
            var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = repository!.FullName,
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("tests/CandidateProgressTracker.IntegrationTests/Browser/course-files.cjs");
            start.Environment["CPT_COURSE_CONFIG"] = JsonSerializer.Serialize(new { password, control, adminName,
                candidateName, mentorName, outsiderName, candidateId, cycleId });
            using var process = System.Diagnostics.Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
            var diagnostics = (await stdout) + Environment.NewLine + (await stderr) + Environment.NewLine + string.Join(Environment.NewLine, errors);
            var output = Path.Combine(repository.FullName, "TestResults", "course-browser"); Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "browser-output.txt"), diagnostics);
            Assert.True(process.ExitCode == 0, diagnostics);
            await using var verify = fixture.Services.CreateAsyncScope();
            var db = verify.ServiceProvider.GetRequiredService<TrackerDbContext>();
            var assignment = await db.Assignments.SingleAsync(a => a.CycleId == cycleId && a.OfferingId != null);
            Assert.Single(await db.Submissions.Where(s => s.AssignmentId == assignment.AssignmentId).ToListAsync());
            Assert.Single(await db.SubmissionReviews.Where(r => r.AssignmentId == assignment.AssignmentId).ToListAsync());
            Assert.Single(await db.UploadAttempts.Where(a => a.AssignmentId == assignment.AssignmentId && a.Status == "Committed").ToListAsync());
        }
        finally
        {
            await live.DisposeAsync();
            // Only this invocation's exact, generated storage root is owned by this test.
            var resolved = Path.GetFullPath(storageRoot);
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
            Assert.True(Guid.TryParseExact(Path.GetFileName(resolved)["CptCourseBrowser_".Length..], "N", out _));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }

    private sealed class ProcessingControl(string secret) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                if (context.Request.Path != "/_acceptance/process") { await continuation(); return; }
                if (context.Request.Method != "POST" || context.Request.Headers["X-Acceptance-Control"] != secret)
                { context.Response.StatusCode = 404; return; }
                var service = context.RequestServices.GetRequiredService<IFileEvidenceService>();
                await service.ProcessNextAsync(); context.Response.StatusCode = 204;
            });
            next(app);
        };
    }
}
