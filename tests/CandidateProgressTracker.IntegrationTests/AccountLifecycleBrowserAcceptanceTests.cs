using System.Security.Cryptography;
using System.Text.Json;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class LifecycleBrowserFactAttribute : FactAttribute
{
    public LifecycleBrowserFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CPT_LIFECYCLE_BROWSER") != "1")
            Skip = "Opt-in installed-Chrome lifecycle acceptance against disposable test accounts.";
    }
}

public sealed class AccountLifecycleBrowserAcceptanceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [LifecycleBrowserFact]
    public async Task Real_browser_lifecycle_journeys_preserve_memberships_audit_and_last_administrator()
    {
        var password = "Accept!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18)) + "a9";
        var nextPassword = "Recover!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18)) + "a9";
        var control = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var candidateName = "accept.candidate." + Guid.NewGuid().ToString("N");
        string adminId, adminName, connectionString;
        Guid cohortId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            Assert.StartsWith("CandidateProgressTracker_Tests_", db.Database.GetDbConnection().Database);
            connectionString = db.Database.GetDbConnection().ConnectionString;
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = await db.Users.SingleAsync(u => u.UserName!.StartsWith("web.Administrator."));
            adminId = admin.Id; adminName = admin.UserName!;
            admin.Email = "accept.admin." + Guid.NewGuid().ToString("N") + "@example.invalid";
            Assert.True((await users.UpdateAsync(admin)).Succeeded);
            Assert.True((await users.RemovePasswordAsync(admin)).Succeeded);
            Assert.True((await users.AddPasswordAsync(admin, password)).Succeeded);
            cohortId = await (from a in db.Assignments join c in db.LearningCycles on a.CycleId equals c.CycleId
                              where a.AssignmentId == fixture.AssignmentId select c.CohortId).SingleAsync();
            Assert.Single(await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                                 where r.Name == "Administrator" select ur.UserId).ToListAsync());
        }
        Guid? membershipId = null;
        byte[]? endedVersion = null;
        // The installed framework supports a real Kestrel server for browser tests.
        // Controls are test-only startup middleware, never production routes.
        Func<HttpContext, Task> controlHandler = async context =>
        {
                if (!HttpMethods.IsPost(context.Request.Method) || context.Request.Headers["X-Acceptance-Control"] != control)
                { context.Response.StatusCode = 404; return; }
                await using var scope = fixture.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
                var owner = await db.Users.SingleAsync(u => u.UserName == candidateName);
                switch (context.Request.Path.Value)
                {
                    case "/_acceptance/expire":
                        var token = await db.AccountCredentials.SingleAsync(c => c.UserId == owner.Id && c.UsedAt == null && c.RevokedAt == null);
                        token.IssuedAt = DateTimeOffset.UtcNow.AddDays(-2);
                        token.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                        await db.SaveChangesAsync();
                        break;
                    case "/_acceptance/membership":
                        Assert.Null(membershipId);
                        var member = new CohortMembership { CohortId = cohortId, UserId = owner.Id, MembershipKind = MembershipKind.Candidate,
                            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedBy = adminId, ChangeReason = "Disposable lifecycle browser acceptance" };
                        db.Add(member); await db.SaveChangesAsync(); membershipId = member.MembershipId;
                        break;
                    case "/_acceptance/end-membership":
                        var row = await db.CohortMemberships.SingleAsync(m => m.MembershipId == membershipId);
                        var result = await scope.ServiceProvider.GetRequiredService<ICohortMembershipEndingService>()
                            .EndAsync(row.MembershipId, "Disposable membership ended before reactivation", row.RowVersion, adminId);
                        Assert.True(result.Succeeded);
                        await db.Entry(row).ReloadAsync(); endedVersion = row.RowVersion.ToArray();
                        break;
                    default: context.Response.StatusCode = 404; return;
                }
                context.Response.StatusCode = 204; return;
        };
        var errorTypes = new System.Collections.Concurrent.ConcurrentQueue<string>();
        await using var networkFactory = new AccessWebFactory(connectionString);
        await using var liveFactory = networkFactory.WithWebHostBuilder(b =>
            b.ConfigureLogging(logging => logging.AddProvider(new SafeErrorTypes(errorTypes)))
             .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new LifecycleControls(controlHandler))));
        liveFactory.UseKestrel(options => options.ListenLocalhost(7264, listen => listen.UseHttps()));
        liveFactory.StartServer();
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
            Assert.NotNull(root);
            var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = root!.FullName, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("tests/CandidateProgressTracker.IntegrationTests/Browser/account-lifecycle.cjs");
            // Secrets remain in process memory. No credentials, DOM dumps, HAR or screenshots are persisted.
            start.Environment["CPT_LIFECYCLE_CONFIG"] = JsonSerializer.Serialize(new { adminName, candidateName, password, nextPassword, control });
            using var process = System.Diagnostics.Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
            var diagnostics = (await stdout) + Environment.NewLine + (await stderr) + Environment.NewLine + string.Join(Environment.NewLine, errorTypes);
            var output = Path.Combine(root.FullName, "TestResults", "lifecycle-browser"); Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "browser-output.txt"), diagnostics);
            Assert.True(process.ExitCode == 0, diagnostics); // Browser diagnostics contain labels only.
        }
        finally { await liveFactory.DisposeAsync(); }

        await using var verify = fixture.Services.CreateAsyncScope();
        var finalDb = verify.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var candidate = await finalDb.Users.SingleAsync(u => u.UserName == candidateName);
        var finalMembership = await finalDb.CohortMemberships.SingleAsync(m => m.MembershipId == membershipId);
        Assert.NotNull(finalMembership.EndsAt); Assert.NotNull(finalMembership.EndedAt);
        Assert.Equal(adminId, finalMembership.EndedBy); Assert.Equal(endedVersion, finalMembership.RowVersion);
        Assert.Single(await finalDb.CohortMemberships.Where(m => m.UserId == candidate.Id).ToListAsync());
        Assert.True(await finalDb.AccountChanges.CountAsync(c => c.UserId == candidate.Id) >= 10);
        var changes = await finalDb.AuditEvents.Where(a => a.ActorId == adminId || a.EntityId == candidate.Id).Select(a => a.Details).ToListAsync();
        Assert.False(changes.Any(text => text.Contains(password) || text.Contains(nextPassword)), "Audit must not contain acceptance passwords.");
        var admins = await (from ur in finalDb.UserRoles join r in finalDb.Roles on ur.RoleId equals r.Id
                            where r.Name == "Administrator" select ur.UserId).ToListAsync();
        Assert.Single(admins); Assert.Equal(adminId, admins[0]);
        Assert.False(await finalDb.AccountAccessStates.AnyAsync(s => s.UserId == adminId && !s.Enabled));
    }
    internal sealed class SafeErrorTypes(System.Collections.Concurrent.ConcurrentQueue<string> output) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ErrorLogger(output);
        public void Dispose() { }
        private sealed class ErrorLogger(System.Collections.Concurrent.ConcurrentQueue<string> output) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Never format request/Identity state, exception messages or form values.
                if (exception is not null && output.Count < 20)
                    output.Enqueue(exception.GetType().Name + " / " + exception.GetBaseException().GetType().Name + Environment.NewLine + exception.GetBaseException().StackTrace);
            }
        }
    }

    private sealed class LifecycleControls(Func<HttpContext, Task> handler) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                if (context.Request.Path.StartsWithSegments("/_acceptance")) await handler(context);
                else await continuation();
            });
            next(app);
        };
    }

}
