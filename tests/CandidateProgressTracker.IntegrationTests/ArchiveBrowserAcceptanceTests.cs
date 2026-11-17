using System.Security.Cryptography;
using System.Text.Json;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class ArchiveBrowserFactAttribute : FactAttribute
{
    public ArchiveBrowserFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CPT_ARCHIVE_BROWSER") != "1")
            Skip = "Opt-in live Chrome archive/audit acceptance with disposable SQL data and private evidence.";
    }
}

public sealed class ArchiveBrowserAcceptanceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [ArchiveBrowserFact]
    public async Task Archive_browser_retains_history_and_evidence_denies_learners_and_searches_audit_without_reopening_approval()
    {
        var password = "Accept!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18)) + "a9";
        var root = Path.Combine(Path.GetTempPath(), "CptArchiveBrowser_" + Guid.NewGuid().ToString("N"));
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "AGENTS.md"))) repository = repository.Parent;
        Assert.NotNull(repository);
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        Assert.StartsWith("CandidateProgressTracker_Tests_", t.Db.Database.GetDbConnection().Database);
        var users = t.Services.GetRequiredService<UserManager<IdentityUser>>();
        var admin = (await users.FindByIdAsync(t.Seed.AdministratorId))!;
        var candidate = (await users.FindByIdAsync(t.Seed.CandidateId))!;
        var mentor = (await users.FindByIdAsync(t.MentorId))!;
        foreach (var user in new[] { admin, candidate, mentor })
        {
            Assert.True((await users.RemovePasswordAsync(user)).Succeeded);
            Assert.True((await users.AddPasswordAsync(user, password)).Succeeded);
        }
        var options = new EvidenceOptions { Root = root, RepositoryRoot = repository!.FullName };
        // ReviewTestContext resolves its evidence verifier from the fixture's DI
        // container. Point that verifier at the same owned store used to prepare
        // the real scanned bytes; the live browser host is configured below.
        var fixtureStorage = fixture.Services.GetRequiredService<IOptions<EvidenceOptions>>().Value;
        fixtureStorage.Root = root;
        fixtureStorage.RepositoryRoot = repository.FullName;
        var files = new FileEvidenceService(t.Db, new PrivateEvidenceStorage(Options.Create(options)),
            new DefenderScanner(), NullLogger<FileEvidenceService>.Instance);
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        await using var factory = new AccessWebFactory(t.Db.Database.GetConnectionString()!);
        await using var live = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(logging => logging.AddProvider(new AccountLifecycleBrowserAcceptanceTests.SafeErrorTypes(errors)))
            .ConfigureServices(services => services.Configure<EvidenceOptions>(o => { o.Root = root; o.RepositoryRoot = repository.FullName; })));
        try
        {
            // Prepare approved work through existing services; this interval tests governance UI,
            // not a duplicate of the already accepted course upload/review browser journey.
            var sample = Path.Combine(repository.FullName, "TestResults", "operational-acceptance", "samples", "certificate.pdf");
            ProcessingReceipt receipt;
            await using (var content = File.OpenRead(sample))
                receipt = await files.ReceiveAsync(new(await t.NewRequestAsync(), "", [new("certificate.pdf", content)]), t.Seed.CandidateId);
            Assert.Equal("Processing", receipt.Status);
            Assert.True(await files.ProcessNextAsync());
            receipt = (await files.GetAsync(receipt.AttemptId, t.Seed.CandidateId))!;
            Assert.True(receipt.Status == "Committed", receipt.Message);
            var fileId = await t.Db.EvidenceFiles.Where(f => f.UploadAttemptId == receipt.AttemptId).Select(f => f.EvidenceFileId).SingleAsync();
            var approval = await t.ApproveAsync();
            var auditIds = await t.Db.AuditEvents.Select(a => a.AuditEventId).ToArrayAsync();
            async Task<string> SnapshotAsync() => JsonSerializer.Serialize(new
            {
                assignments = await t.Db.Assignments.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).ToListAsync(),
                revisions = await t.Db.AssignmentRevisions.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).OrderBy(a => a.RevisionNo).ToListAsync(),
                submissions = await t.Db.Submissions.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).OrderBy(a => a.AttemptNo).ToListAsync(),
                reviews = await t.Db.SubmissionReviews.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).OrderBy(a => a.ReviewNo).ToListAsync(),
                evidence = await t.Db.SubmissionEvidence.AsNoTracking().OrderBy(a => a.SubmissionEvidenceId).ToListAsync(),
                files = await t.Db.EvidenceFiles.AsNoTracking().OrderBy(a => a.EvidenceFileId).ToListAsync(),
                uploads = await t.Db.UploadAttempts.AsNoTracking().OrderBy(a => a.UploadAttemptId).ToListAsync(),
                audit = await t.Db.AuditEvents.AsNoTracking().Where(a => auditIds.Contains(a.AuditEventId)).OrderBy(a => a.AuditEventId).ToListAsync()
            });
            var before = await SnapshotAsync();
            live.UseKestrel(o => o.ListenLocalhost(7265, l => l.UseHttps())); live.StartServer();
            var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = repository.FullName,
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("tests/CandidateProgressTracker.IntegrationTests/Browser/course-files.cjs");
            start.Environment["CPT_COURSE_CONFIG"] = JsonSerializer.Serialize(new { journey = "archive", password,
                adminName = admin.UserName, candidateName = candidate.UserName, mentorName = mentor.UserName,
                adminId = admin.Id, projectId = t.Seed.ProjectId, assignmentId = t.Seed.AssignmentId, fileId, receiptId = receipt.SubmissionId });
            using var process = System.Diagnostics.Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
            var output = Path.Combine(repository.FullName, "TestResults", "archive-browser"); Directory.CreateDirectory(output);
            var diagnostics = (await stdout) + Environment.NewLine + (await stderr) + Environment.NewLine + string.Join(Environment.NewLine, errors);
            await File.WriteAllTextAsync(Path.Combine(output, "browser-output.txt"), diagnostics);
            Assert.True(process.ExitCode == 0, diagnostics);
            Assert.Equal(before, await SnapshotAsync());
            var events = await t.Db.ScopeArchiveEvents.AsNoTracking().Where(e => e.ScopeId == t.Seed.ProjectId).OrderBy(e => e.OccurredAt).ToListAsync();
            Assert.Equal(2, events.Count); Assert.True(events[0].Archived); Assert.False(events[1].Archived);
            Assert.Equal("Browser archive acceptance", events[0].Reason);
            Assert.Equal("Browser scope reopening acceptance", events[1].Reason);
            foreach (var entry in events)
                Assert.Contains(JsonSerializer.Deserialize<List<ArchiveOutcome>>(entry.OutcomeSnapshot)!, o => o.AssignmentId == t.Seed.AssignmentId && o.ReceiptId == receipt.SubmissionId && o.Approved);
            Assert.Empty(await t.Db.AssignmentReopenings.Where(a => a.AssignmentId == t.Seed.AssignmentId).ToListAsync());
            Assert.Equal(approval.ReceiptId, (await t.Reopenings.GetReopeningAsync(t.Seed.AssignmentId, admin.Id))!.EffectiveApprovalId);
            var denied = await t.Submissions.SubmitAsync(await t.NewRequestAsync(), candidate.Id);
            Assert.False(denied.Succeeded); Assert.Contains(denied.Errors, e => e.Contains("approval", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(before, await SnapshotAsync());
        }
        finally
        {
            await live.DisposeAsync();
            var resolved = Path.GetFullPath(root);
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
            Assert.True(Guid.TryParseExact(Path.GetFileName(resolved)["CptArchiveBrowser_".Length..], "N", out _));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
}
