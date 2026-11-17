using System.Net;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using CandidateProgressTracker.Web;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class OperationalFactAttribute : FactAttribute
{
    public OperationalFactAttribute()
    { if(!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CPT_RUN_OPERATIONAL_ACCEPTANCE")!="1") Skip="Opt-in Windows Defender/private-filesystem acceptance, not the ordinary suite."; }
}
public sealed class CourseFileOperationalAcceptanceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>, IDisposable
{
    private readonly string root=Path.Combine(Environment.GetEnvironmentVariable("CPT_EVIDENCE_ACCEPTANCE_ROOT") ?? Path.GetTempPath(),"CptOperational_"+Guid.NewGuid().ToString("N"));
    private PrivateEvidenceStorage Storage=>new(Options.Create(new EvidenceOptions {Root=root,RepositoryRoot=Directory.GetCurrentDirectory()}));
    private FileEvidenceService Service(TrackerDbContext db,IMalwareScanner? scanner=null)=>new(db,Storage,scanner??new DefenderScanner(),NullLogger<FileEvidenceService>.Instance);
    private static string Samples=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../TestResults/operational-acceptance/samples"));
    private async Task<IndividualSubmissionTests.Seed> CourseAsync(TrackerDbContext db)
    {
        var s=await new IndividualSubmissionTests(fixture).SeedAsync(db);var cycle=await db.Assignments.Where(a=>a.AssignmentId==s.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        var courses=new CourseService(db);var title="Operational certificate "+Guid.NewGuid().ToString("N");
        Assert.Null(await courses.CreateAsync(title,"Acceptance provider","https://example.com/course",s.AdministratorId));var c=await db.Courses.SingleAsync(c=>c.Title==title);
        Assert.Null(await courses.OfferAsync(c.CourseId,cycle,s.AdministratorId));var offering=await db.CourseOfferings.SingleAsync(o=>o.CourseId==c.CourseId);
        Assert.Null(await courses.AssignAsync(offering.OfferingId,s.CandidateId,DateTimeOffset.UtcNow.AddDays(2),"Complete course","Certificate","Upload certificate",true,s.AdministratorId));
        var a=await db.Assignments.SingleAsync(a=>a.OfferingId==offering.OfferingId);var r=await db.AssignmentRevisions.SingleAsync(r=>r.AssignmentId==a.AssignmentId);
        fixture.Services.GetRequiredService<IOptions<EvidenceOptions>>().Value.Root=root;
        return s with {AssignmentId=a.AssignmentId,RevisionId=r.RevisionId};
    }
    private static FileSubmissionRequest Request(IndividualSubmissionTests.Seed s,string extension)=>new(new(s.AssignmentId,s.RevisionId,Guid.NewGuid(),"Fictional operational certificate",[]),"",[new("certificate."+extension,File.OpenRead(Path.Combine(Samples,"certificate."+extension)))]);
    private static async Task<ProcessingReceipt> Receive(FileEvidenceService service,FileSubmissionRequest request,string actor)
    {try{return await service.ReceiveAsync(request,actor);}finally{foreach(var file in request.Files) await file.Content.DisposeAsync();}}
    private sealed class WorkerObservation
    {
        internal readonly TaskCompletionSource Idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Processed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    // Observe completed real processing calls; never invoke work from the test or replace scanning.
    private sealed class ObservedFiles(FileEvidenceService actual, WorkerObservation observation) : IFileEvidenceService
    {
        public Task<ProcessingReceipt> ReceiveAsync(FileSubmissionRequest request,string actor,CancellationToken ct=default)=>actual.ReceiveAsync(request,actor,ct);
        public Task<ProcessingReceipt?> GetAsync(Guid id,string actor)=>actual.GetAsync(id,actor);
        public Task<EvidenceDownload?> DownloadAsync(Guid id,string actor,CancellationToken ct=default)=>actual.DownloadAsync(id,actor,ct);
        public Task CleanupAsync(CancellationToken ct=default)=>actual.CleanupAsync(ct);
        public async Task<bool> ProcessNextAsync(CancellationToken ct=default)
        {
            var processed=await actual.ProcessNextAsync(ct);
            if(processed) observation.Processed.TrySetResult(); else observation.Idle.TrySetResult();
            return processed;
        }
    }
    private IHost WorkerHost(string connection,WorkerObservation observation)
    {
        Assert.StartsWith("CandidateProgressTracker_Tests_",new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog);
        return new HostBuilder().ConfigureServices(services=>
        {
            services.AddLogging();
            services.AddDbContext<TrackerDbContext>(o=>o.UseSqlServer(connection));
            services.Configure<EvidenceOptions>(o=>{o.Root=root;o.RepositoryRoot=Directory.GetCurrentDirectory();});
            services.AddSingleton<IPrivateEvidenceStorage,PrivateEvidenceStorage>();
            services.AddSingleton<IMalwareScanner,DefenderScanner>();
            services.AddSingleton(observation);
            services.AddScoped<FileEvidenceService>();
            services.AddScoped<IFileEvidenceService,ObservedFiles>();
            services.AddHostedService<EvidenceWorker>();
        }).Build();
    }
    [OperationalFact]
    public async Task Hosted_worker_polls_real_Defender_jobs_and_restart_recovers_expired_lease_without_duplicate_receipts()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await CourseAsync(db);
        var connection=db.Database.GetConnectionString()!;var service=Service(db);
        var firstRequest=Request(seed,"pdf");ProcessingReceipt first,committed;
        var firstSignals=new WorkerObservation();
        using(var host=WorkerHost(connection,firstSignals))
        {
            await host.StartAsync();
            try
            {
                // Wait for a real idle poll, then upload. The unchanged 15-second
                // worker schedule must discover it; no arbitrary sleeps/manual trigger.
                await firstSignals.Idle.Task.WaitAsync(TimeSpan.FromMinutes(2));
                first=await Receive(service,firstRequest,seed.CandidateId);Assert.Equal("Processing",first.Status);
                var file=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==first.AttemptId);
                Assert.Null(await service.DownloadAsync(file.EvidenceFileId,seed.CandidateId));
                await firstSignals.Processed.Task.WaitAsync(TimeSpan.FromMinutes(2));
                db.ChangeTracker.Clear();committed=(await service.GetAsync(first.AttemptId,seed.CandidateId))!;
                Assert.True(committed.Status=="Committed",committed.Message);
            }
            finally {await host.StopAsync();}
        }
        // Leave a second durable upload while the host is stopped. Only this
        // disposable lease timestamp is simulated, not a process crash or clock change.
        var second=await Receive(service,Request(seed,"pdf"),seed.CandidateId);Assert.Equal("Processing",second.Status);
        var pending=await db.UploadAttempts.SingleAsync(a=>a.UploadAttemptId==second.AttemptId);
        pending.LeaseId=Guid.NewGuid();pending.LeaseUntil=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
        var secondFile=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==second.AttemptId);
        Assert.Null(await service.DownloadAsync(secondFile.EvidenceFileId,seed.CandidateId));
        var restartSignals=new WorkerObservation();
        using(var restarted=WorkerHost(connection,restartSignals))
        {
            await restarted.StartAsync();
            try {await restartSignals.Processed.Task.WaitAsync(TimeSpan.FromMinutes(2));}
            finally {await restarted.StopAsync();}
        }
        db.ChangeTracker.Clear();var recovered=(await service.GetAsync(second.AttemptId,seed.CandidateId))!;
        Assert.True(recovered.Status=="Committed",recovered.Message);
        var retry=await Receive(service,new(firstRequest.Content,"",[new("certificate.pdf",File.OpenRead(Path.Combine(Samples,"certificate.pdf")))]),seed.CandidateId);
        Assert.Equal(committed.AttemptId,retry.AttemptId);Assert.Equal(committed.SubmissionId,retry.SubmissionId);Assert.Equal("Committed",retry.Status);
        Assert.Equal(new[]{1,2},await db.Submissions.Where(s=>s.AssignmentId==seed.AssignmentId).OrderBy(s=>s.AttemptNo).Select(s=>s.AttemptNo).ToArrayAsync());
        Assert.Equal(2,await db.UploadAttempts.CountAsync(a=>a.AssignmentId==seed.AssignmentId));
        var ownedFiles=db.EvidenceFiles.AsNoTracking().Where(f=>db.UploadAttempts.Any(a=>a.UploadAttemptId==f.UploadAttemptId&&a.AssignmentId==seed.AssignmentId));
        Assert.Equal(2,await ownedFiles.CountAsync());
        foreach(var file in await ownedFiles.ToListAsync())
        {
            Assert.Equal("Clean",file.ScanResult);Assert.NotNull(file.ScannedAt);
            var downloaded=await service.DownloadAsync(file.EvidenceFileId,seed.CandidateId);Assert.NotNull(downloaded);
            await using var bytes=downloaded.Content;using var buffer=new MemoryStream();await bytes.CopyToAsync(buffer);
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(Samples,"certificate.pdf")),buffer.ToArray());
        }
    }
    [OperationalFact]
    public async Task Real_Defender_processes_PDF_PNG_JPEG_and_protected_downloads_preserve_exact_bytes()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await CourseAsync(db);var service=Service(db);
        using var owner=await fixture.CreateClientAsync("Candidate",seed.CandidateId);using var unassigned=await fixture.CreateClientAsync("Mentor");
        foreach(var extension in new[]{"pdf","png","jpg"})
        {
            var receipt=await Receive(service,Request(seed,extension),seed.CandidateId);Assert.Equal("Processing",receipt.Status);
            var file=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==receipt.AttemptId);Assert.Null(await service.DownloadAsync(file.EvidenceFileId,seed.CandidateId));
            Assert.True(await service.ProcessNextAsync());var status=(await service.GetAsync(receipt.AttemptId,seed.CandidateId))!;Assert.True(status.Status=="Committed",status.Message);
            using var download=await owner.GetAsync($"/Evidence/Download/{file.EvidenceFileId}");Assert.Equal(HttpStatusCode.OK,download.StatusCode);
            Assert.Equal("attachment",download.Content.Headers.ContentDisposition!.DispositionType);Assert.Contains("no-store",download.Headers.CacheControl!.ToString());
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(Samples,"certificate."+extension)),await download.Content.ReadAsByteArrayAsync());
            using var denied=await unassigned.GetAsync($"/Evidence/Download/{file.EvidenceFileId}");Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        }
    }
    private sealed class CancelledDefender : IMalwareScanner
    {public Task<MalwareResult> ScanAsync(Stream content,CancellationToken ct)=>new DefenderScanner().ScanAsync(content,new CancellationToken(true));}
    [OperationalFact]
    public async Task Real_scanner_failure_blocks_files_and_retry_recovers_without_duplicate_submission()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await CourseAsync(db);
        var receipt=await Receive(Service(db),Request(seed,"pdf"),seed.CandidateId);Assert.Equal("Processing",receipt.Status);
        Assert.True(await Service(db,new CancelledDefender()).ProcessNextAsync());var a=(await db.UploadAttempts.FindAsync(receipt.AttemptId))!;
        Assert.Equal("Processing",a.Status);Assert.Contains("unavailable",a.Message);Assert.False(await db.Submissions.AnyAsync(s=>s.AssignmentId==seed.AssignmentId));
        var file=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==a.UploadAttemptId);Assert.Null(await Service(db).DownloadAsync(file.EvidenceFileId,seed.CandidateId));
        a.NextTryAt=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();Assert.True(await Service(db).ProcessNextAsync());Assert.Equal("Committed",a.Status);
        Assert.Single(await db.Submissions.Where(s=>s.AssignmentId==seed.AssignmentId).ToListAsync());
    }
    private sealed class RevisionAfterRealScan(Func<Task> revise) : IMalwareScanner
    {
        public async Task<MalwareResult> ScanAsync(Stream content,CancellationToken ct)
        {var result=await new DefenderScanner().ScanAsync(content,ct);if(result==MalwareResult.Clean) await revise();return result;}
    }
    [OperationalFact]
    public async Task Revision_between_real_scan_and_finalization_rejects_refresh_without_publishing_evidence()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await CourseAsync(db);
        var receipt=await Receive(Service(db),Request(seed,"jpg"),seed.CandidateId);Assert.Equal("Processing",receipt.Status);
        async Task Revise()
        {
            await using var writer=fixture.Services.CreateAsyncScope();var revisions=writer.ServiceProvider.GetRequiredService<IAssignmentRevisionService>();var page=(await revisions.GetAsync(seed.AssignmentId,seed.AdministratorId))!;
            var result=await revisions.ReviseAsync(seed.AssignmentId,page.RowVersion,page.DueAt.AddDays(1),page.Instructions,page.AcceptanceExpectations,page.EvidencePolicy,"Operational stale-finalization check",seed.AdministratorId);
            Assert.True(result.Succeeded,string.Join("; ",result.Errors));
        }
        Assert.True(await Service(db,new RevisionAfterRealScan(Revise)).ProcessNextAsync());var a=(await db.UploadAttempts.FindAsync(receipt.AttemptId))!;
        Assert.Equal("Rejected",a.Status);Assert.Contains("Refresh",a.Message);Assert.False(await db.Submissions.AnyAsync(s=>s.AssignmentId==seed.AssignmentId));
        var file=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==a.UploadAttemptId);Assert.Null(await Service(db).DownloadAsync(file.EvidenceFileId,seed.CandidateId));
    }
    public void Dispose()
    {
        var path=Path.GetFullPath(root);if(!Guid.TryParseExact(Path.GetFileName(path)["CptOperational_".Length..],"N",out _)) throw new InvalidOperationException("Uncertain acceptance directory");
        if(Directory.Exists(path)) Directory.Delete(path,true);
    }
}
