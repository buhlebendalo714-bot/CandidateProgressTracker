using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class FileEvidenceRaceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>, IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"CptEvidenceRaces_"+Guid.NewGuid().ToString("N"));
    private sealed class Scanner : IMalwareScanner {public Task<MalwareResult> ScanAsync(Stream s,CancellationToken ct)=>Task.FromResult(MalwareResult.Clean);}
    private IPrivateEvidenceStorage Storage=>new PrivateEvidenceStorage(Options.Create(new EvidenceOptions {Root=root,RepositoryRoot=Directory.GetCurrentDirectory()}));
    private FileEvidenceService Service(TrackerDbContext db)=>new(db,Storage,new Scanner(),NullLogger<FileEvidenceService>.Instance);
    public void Dispose() {var path=Path.GetFullPath(root);if(!path.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(path)["CptEvidenceRaces_".Length..],"N",out _)) throw new InvalidOperationException();if(Directory.Exists(path)) Directory.Delete(path,true);}
    private static FileSubmissionRequest FileRequest(TeamSubmissionRequest request)=>new(request.Content with {Evidence=[]},request.RosterConfirmation,[new("evidence.png",new MemoryStream(FileEvidenceTests.Png))]);
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Concurrent_receipts_and_workers_freeze_one_roster_and_never_duplicate_submission(bool distinct)
    {
        await using var t=await TeamTestContext.CreateAsync(fixture);var original=await t.RequestAsync();
        var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var arrived=0;
        var ids=new System.Collections.Concurrent.ConcurrentBag<Guid>();
        async Task<ProcessingReceipt> Receive(int n)
        {
            await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();ids.Add(db.ContextId.InstanceId);
            var request=distinct && n==1 ? original with {Content=original.Content with {RequestId=Guid.NewGuid()}} : original;
            if(Interlocked.Increment(ref arrived)==2) ready.SetResult();await start.Task;
            return await Service(db).ReceiveAsync(FileRequest(request),t.Seed.CandidateId);
        }
        var tasks=new[]{Receive(0),Receive(1)};await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));start.SetResult();var receipts=await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(90));Assert.Equal(2,ids.Distinct().Count());
        Assert.Contains(receipts,r=>r.Status=="Processing");
        // An explicit retryable conflict is acceptable. Replay only requests that were accepted.
        var accepted=receipts.Where(r=>r.AttemptId!=Guid.Empty).Select(r=>r.AttemptId).Distinct().ToArray();
        var barrier=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var workerReady=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var workers=0;
        async Task Work()
        {
            await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            if(Interlocked.Increment(ref workers)==2) workerReady.SetResult();await barrier.Task;await Service(db).ProcessNextAsync();
        }
        var work=new[]{Work(),Work()};await workerReady.Task.WaitAsync(TimeSpan.FromSeconds(30));barrier.SetResult();await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(90));
        // READPAST/leases may legitimately let one concurrent claimant return no work.
        // Complete bounded recovery passes instead of requiring immediate success.
        for(var recovery=0;recovery<4 && await t.Db.UploadAttempts.AsNoTracking().AnyAsync(a=>a.AssignmentId==t.Seed.AssignmentId && a.Status=="Processing");recovery++)
        {
            await using var retryScope=fixture.Services.CreateAsyncScope();
            await Service(retryScope.ServiceProvider.GetRequiredService<TrackerDbContext>()).ProcessNextAsync();
        }
        var remaining=await t.Db.UploadAttempts.AsNoTracking().Where(a=>a.AssignmentId==t.Seed.AssignmentId && a.Status=="Processing").Select(a=>a.Message).ToListAsync();
        Assert.True(remaining.Count==0,string.Join("; ",remaining));
        var submissions=await t.Db.Submissions.AsNoTracking().Where(s=>s.AssignmentId==t.Seed.AssignmentId).ToListAsync();Assert.Single(submissions);Assert.Equal(1,submissions[0].AttemptNo);
        Assert.Single(await t.Db.AssignmentCreditedRosters.Where(r=>r.AssignmentId==t.Seed.AssignmentId).ToListAsync());Assert.Equal(2,await t.Db.AssignmentCreditedRosterMembers.CountAsync(m=>m.AssignmentId==t.Seed.AssignmentId));
        Assert.Single(await (from f in t.Db.EvidenceFiles join a in t.Db.UploadAttempts on f.UploadAttemptId equals a.UploadAttemptId where a.SubmissionId==submissions[0].SubmissionId select f).ToListAsync());
        var reviewService=new IndividualSubmissionReviewService(t.Db,Storage);var page=(await reviewService.GetReviewAsync(t.Seed.AssignmentId,t.MentorId))!;Assert.True(page.CanDecide);
        await t.EndTeamAsync(t.OtherId);var file=await (from f in t.Db.EvidenceFiles join a in t.Db.UploadAttempts on f.UploadAttemptId equals a.UploadAttemptId where a.SubmissionId==submissions[0].SubmissionId select f).SingleAsync();
        Assert.Null(await Service(t.Db).DownloadAsync(file.EvidenceFileId,t.OtherId));Assert.NotNull(await Service(t.Db).DownloadAsync(file.EvidenceFileId,t.Seed.CandidateId));
    }
    [Fact]
    public async Task Roster_change_during_processing_rejects_without_freezing_and_new_confirmation_can_succeed()
    {
        await using var t=await TeamTestContext.CreateAsync(fixture);var request=await t.RequestAsync();var service=Service(t.Db);
        var receipt=await service.ReceiveAsync(FileRequest(request),t.Seed.CandidateId);Assert.Equal("Processing",receipt.Status);
        await t.EndTeamAsync(t.OtherId);Assert.True(await service.ProcessNextAsync());Assert.Equal("Rejected",(await service.GetAsync(receipt.AttemptId,t.Seed.CandidateId))!.Status);
        Assert.False(await t.Db.AssignmentCreditedRosters.AnyAsync(r=>r.AssignmentId==t.Seed.AssignmentId));Assert.False(await t.Db.Submissions.AnyAsync(s=>s.AssignmentId==t.Seed.AssignmentId));
        var fresh=await t.RequestAsync();var accepted=await service.ReceiveAsync(FileRequest(fresh),t.Seed.CandidateId);Assert.Equal("Processing",accepted.Status);Assert.True(await service.ProcessNextAsync());
        Assert.Single(await t.Db.AssignmentCreditedRosterMembers.Where(m=>m.AssignmentId==t.Seed.AssignmentId).ToListAsync());
    }
    private sealed class FailAfterSubmission : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e,int result,CancellationToken ct=default)
        {if(e.Context!.ChangeTracker.Entries<Submission>().Any()) throw new IOException("Injected transaction failure after submission write");return ValueTask.FromResult(result);}
    }
    [Fact]
    public async Task Failed_finalization_rolls_back_roster_attempt_files_and_assignment_version_and_lease_can_recover()
    {
        await using var t=await TeamTestContext.CreateAsync(fixture);var received=await Service(t.Db).ReceiveAsync(FileRequest(await t.RequestAsync()),t.Seed.CandidateId);Assert.Equal("Processing",received.Status);
        var version=await t.Db.Assignments.AsNoTracking().Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.RowVersion).SingleAsync();
        var options=new DbContextOptionsBuilder<TrackerDbContext>(t.Services.GetRequiredService<DbContextOptions<TrackerDbContext>>()).AddInterceptors(new FailAfterSubmission()).Options;
        await using(var db=new TrackerDbContext(options)) await Assert.ThrowsAsync<IOException>(()=>Service(db).ProcessNextAsync());
        Assert.False(await t.Db.Submissions.AnyAsync(s=>s.AssignmentId==t.Seed.AssignmentId));Assert.False(await t.Db.AssignmentCreditedRosters.AnyAsync(r=>r.AssignmentId==t.Seed.AssignmentId));
        Assert.Equal(version,await t.Db.Assignments.AsNoTracking().Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.RowVersion).SingleAsync());
        var attempt=(await t.Db.UploadAttempts.FindAsync(received.AttemptId))!;await t.Db.Entry(attempt).ReloadAsync();Assert.Equal("Processing",attempt.Status);attempt.LeaseUntil=DateTimeOffset.UtcNow.AddSeconds(-1);await t.Db.SaveChangesAsync();
        Assert.True(await Service(t.Db).ProcessNextAsync());Assert.Equal("Committed",(await Service(t.Db).GetAsync(received.AttemptId,t.Seed.CandidateId))!.Status);
    }
}
