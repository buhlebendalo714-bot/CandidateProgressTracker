using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace CandidateProgressTracker.Infrastructure.Learning;
public sealed class FileEvidenceService(TrackerDbContext db, IPrivateEvidenceStorage storage, IMalwareScanner scanner, ILogger<FileEvidenceService> log) : IFileEvidenceService
{
    private static ProcessingReceipt Receipt(UploadAttempt a) => new(a.UploadAttemptId,a.Status,a.Message,a.SubmissionId);
    private static ProcessingReceipt Rejected(string message) => new(Guid.Empty,"Rejected",message,null);
    private async Task<AssignmentAccess.Target?> CandidateAsync(Guid id, string actor, bool submit)
    {
        var t=await AssignmentAccess.TargetAsync(db,id);
        return t is not null && await AssignmentAccess.CandidateAsync(db,t,actor,submit) ? t : null;
    }
    public async Task<ProcessingReceipt> ReceiveAsync(FileSubmissionRequest request,string actor,CancellationToken ct=default)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromMinutes(10)); ct=deadline.Token;
        if(request is null || request.Content is null || request.Files is null || request.Content.Evidence is null || string.IsNullOrWhiteSpace(actor)) return Rejected("This upload form is no longer valid. Reload the assignment and try again.");
        var c=request.Content;
        var explanation=c.Explanation?.Trim() ?? "";
        if(c.RequestId==Guid.Empty || explanation.Length is <1 or >10000 || request.Files.Count is <1 or >FileLimits.Count || c.Evidence.Count>10) return Rejected("Enter an explanation and select 1 to 5 PDF, PNG or JPEG files, up to 10 MB each.");
        var links=c.Evidence.Select(e=>new SubmissionEvidenceInput(e?.Label?.Trim() ?? "",e?.Url?.Trim() ?? "")).ToList();
        if(links.Any(e=>e.Label.Length is <1 or >200 || e.Url.Length>2000 || !new SubmissionHistoryEvidence(e.Label,e.Url).IsClickable)
            || links.Select(e=>e.Url).Distinct(StringComparer.Ordinal).Count()!=links.Count) return Rejected("Enter a valid https:// address for each link and remove duplicate links.");
        // Preauthorize before reading potentially large/untrusted streams. Recheck at receipt and finalization.
        if(await CandidateAsync(c.AssignmentId,actor,true) is null) return Rejected("This assignment is not available.");
        var objects=new List<StoredEvidence>();
        try
        {
            foreach(var file in request.Files)
            {
                objects.Add(await storage.StoreAsync(file.Name,file.Content,ct));
                if(objects.Sum(f=>f.Length)>FileLimits.Total) throw new InvalidDataException("The files together must not exceed 50 MB.");
            }
            var received=DateTimeOffset.UtcNow;
            var payload=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
                c.AssignmentId,c.AssignmentRevisionId,Explanation=explanation,Links=links,
                Files=objects.Select(f=>new {f.Name,f.Length,f.Hash,f.MediaType}) }))));
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await AssignmentWorkflow.LockAsync(db,c.AssignmentId);
            var t=await CandidateAsync(c.AssignmentId,actor,true);
            if(t is null) return Rejected("You no longer have permission to submit this assignment. Contact your Administrator if you need access.");
            var existing=await db.UploadAttempts.SingleOrDefaultAsync(a=>a.ActorId==actor && a.RequestId==c.RequestId,ct);
            if(existing is not null) return existing.PayloadHash==payload && existing.AssignmentId==c.AssignmentId
                ? Receipt(existing) : Rejected("This form was already used for different content. Reload it before submitting your changes.");
            if(await db.Submissions.AnyAsync(s=>s.SubmittedBy==actor && s.RequestId==c.RequestId,ct)) return Rejected("This form was already used for different content. Reload it before submitting your changes.");
            var state=await AssignmentWorkflow.LoadAsync(db,c.AssignmentId);
            if(state.EffectiveApproval is not null) return Rejected("This assignment is approved. New submissions are blocked until an Administrator reopens the approval or the requirements change.");
            if(state.Revisions.LastOrDefault()?.RevisionId!=c.AssignmentRevisionId) return Rejected("Requirements changed. Refresh and resubmit.");
            if(t.Assignment.TeamId is not null && !await RosterMatchesAsync(t,actor,request.RosterConfirmation)) return Rejected("Team members changed. Refresh the form, check the included members and confirm again.");
            var a=new UploadAttempt { AssignmentId=c.AssignmentId,RevisionId=c.AssignmentRevisionId,ActorId=actor,RequestId=c.RequestId,
                Explanation=explanation,LinksJson=JsonSerializer.Serialize(links),PayloadHash=payload,RosterConfirmation=request.RosterConfirmation,
                ExpectedVersion=t.Assignment.RowVersion,ReceivedAt=received,NextTryAt=received };
            db.UploadAttempts.Add(a);
            for(var i=0;i<objects.Count;i++) { var f=objects[i]; db.EvidenceFiles.Add(new() {UploadAttemptId=a.UploadAttemptId,SequenceNo=i+1,
                ObjectKey=f.Key,OriginalName=f.Name,MediaType=f.MediaType,SizeBytes=f.Length,Sha256=f.Hash}); }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); objects.Clear(); return Receipt(a);
        }
        catch(Exception e) when(e is InvalidDataException or IOException or UnauthorizedAccessException or OperationCanceledException || AssignmentWorkflow.Retryable(e))
        { log.LogWarning("Evidence receipt failed: {FailureType}",e.GetType().Name); return Rejected(e is InvalidDataException ? e.Message : "The upload could not be received safely. Check your files and try submitting this form again. If the problem continues, contact your Administrator."); }
        finally
        {
            // Do not delete after an ambiguous commit: durable rows always win. Unknown
            // storage failures are left for reconciliation, never falsely reported as submitted.
            foreach(var f in objects)
            {
                try { if(!await db.EvidenceFiles.AsNoTracking().AnyAsync(x=>x.ObjectKey==f.Key)) await storage.DeleteAsync(f.Key,CancellationToken.None); }
                catch(Exception e) when(e is IOException or UnauthorizedAccessException || e is Microsoft.Data.SqlClient.SqlException) { log.LogWarning("Staged evidence cleanup deferred."); }
            }
        }
    }
    private async Task<bool> RosterMatchesAsync(AssignmentAccess.Target t,string actor,string confirmation)
    {
        var team=await TeamSubmissionAccess.TargetAsync(db,t.Assignment.AssignmentId); if(team is null) return false;
        var roster=await TeamSubmissionAccess.RosterAsync(db,t.Assignment.AssignmentId);
        var preview=roster?.Members ?? await TeamSubmissionAccess.EligibleAsync(db,team,DateTimeOffset.UtcNow);
        return preview.Any(m=>m.UserId==actor) && confirmation==TeamSubmissionAccess.Fingerprint(t.Assignment.AssignmentId,preview);
    }
    public async Task<ProcessingReceipt?> GetAsync(Guid id,string actor)
    {
        var a=await db.UploadAttempts.AsNoTracking().SingleOrDefaultAsync(a=>a.UploadAttemptId==id && a.ActorId==actor);
        return a is not null && await CandidateAsync(a.AssignmentId,actor,false) is not null ? Receipt(a) : null;
    }
    public async Task<EvidenceDownload?> DownloadAsync(Guid id,string actor,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var f=await db.EvidenceFiles.AsNoTracking().SingleOrDefaultAsync(f=>f.EvidenceFileId==id && !f.Deleted && f.ScanResult=="Clean",ct);
        if(f is null) return null;
        var a=await db.UploadAttempts.AsNoTracking().SingleAsync(a=>a.UploadAttemptId==f.UploadAttemptId,ct);
        if(a.Status!="Committed" || a.SubmissionId is null) return null;
        var administrator=await AssignmentAccess.RoleAsync(db,actor,"ADMINISTRATOR");
        var t=await AssignmentAccess.TargetAsync(db,a.AssignmentId,administrator);
        if(t is null || !(administrator || await AssignmentAccess.CandidateAsync(db,t,actor,false) || await AssignmentAccess.StaffAsync(db,t,actor,"MENTOR"))) return null;
        try { var bytes=await storage.OpenVerifiedAsync(f.ObjectKey,f.SizeBytes,f.Sha256,ct); await tx.CommitAsync(ct); return new(bytes,f.OriginalName,f.MediaType); }
        catch(IOException) { log.LogError("Missing/corrupt evidence {FileId}",id); return null; }
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct=default)
    {
        UploadAttempt? job; var lease=Guid.NewGuid();
        await using(var claim=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct))
        {
            job=await db.UploadAttempts.FromSqlRaw("SELECT TOP(1) * FROM UploadAttempts WITH (UPDLOCK, READPAST, ROWLOCK) WHERE Status='Processing' AND ReceivedAt>DATEADD(hour,-24,SYSUTCDATETIME()) AND NextTryAt<=SYSUTCDATETIME() AND (LeaseUntil IS NULL OR LeaseUntil<SYSUTCDATETIME()) ORDER BY ReceivedAt").SingleOrDefaultAsync(ct);
            if(job is null) return false;
            job.LeaseId=lease; job.LeaseUntil=DateTimeOffset.UtcNow.AddMinutes(10); await db.SaveChangesAsync(ct); await claim.CommitAsync(ct);
        }
        var files=await db.EvidenceFiles.Where(f=>f.UploadAttemptId==job.UploadAttemptId).OrderBy(f=>f.SequenceNo).ToListAsync(ct);
        var outcome=MalwareResult.Clean;
        try
        {
            if(files.Count is <1 or >5) outcome=MalwareResult.Unsafe;
            foreach(var f in files)
            {
                await using var bytes=await storage.OpenVerifiedAsync(f.ObjectKey,f.SizeBytes,f.Sha256,ct);
                var result=await scanner.ScanAsync(bytes,ct);
                if(result!=MalwareResult.Clean) { outcome=result; break; }
            }
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or OperationCanceledException) { outcome=MalwareResult.Unavailable; }
        // A lease may expire while scanning; a later owner is the only allowed finalizer.
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await AssignmentWorkflow.LockAsync(db,job.AssignmentId);
        await db.Entry(job).ReloadAsync(ct);
        if(job.Status!="Processing" || job.LeaseId!=lease || job.LeaseUntil<=DateTimeOffset.UtcNow) return true;
        job.LeaseUntil=null; job.LeaseId=null;
        if(outcome!=MalwareResult.Clean)
        {
            job.Failures++;
            if(outcome==MalwareResult.Unsafe || job.Failures>=5) { job.Status="Rejected"; job.FinishedAt=DateTimeOffset.UtcNow; job.Message="The files were not accepted or could not be checked. Return to the assignment and upload valid PDF, PNG or JPEG files again."; }
            else { job.NextTryAt=DateTimeOffset.UtcNow.AddMinutes(Math.Min(60,job.Failures*2)); job.Message="File checks are temporarily unavailable. Your upload will be retried automatically. Refresh this page later; it has not been submitted yet."; }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        var target=await CandidateAsync(job.AssignmentId,job.ActorId,true);
        var state=await AssignmentWorkflow.LoadAsync(db,job.AssignmentId);
        var reason=target is null ? "Current authorization no longer permits submission."
            : state.Revisions.LastOrDefault()?.RevisionId!=job.RevisionId ? "Assignment revision changed. Refresh and resubmit."
            : !target.Assignment.RowVersion.SequenceEqual(job.ExpectedVersion) ? "Assignment changed while processing. Refresh and resubmit."
            : state.EffectiveApproval is not null ? "This assignment is approved. New submissions are blocked until an Administrator reopens the approval or the requirements change."
            : target.Assignment.TeamId is not null && !await RosterMatchesAsync(target,job.ActorId,job.RosterConfirmation) ? "Roster eligibility changed. Refresh and confirm the roster again." : null;
        if(reason is not null) { job.Status="Rejected"; job.FinishedAt=DateTimeOffset.UtcNow; job.Message=reason; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true; }
        // Reopen and verify each exact object before exposing a reference. Object keys
        // are immutable and the service never overwrites an existing object.
        foreach(var f in files) { await using var check=await storage.OpenVerifiedAsync(f.ObjectKey,f.SizeBytes,f.Sha256,ct); f.ScanResult="Clean"; f.ScannedAt=DateTimeOffset.UtcNow; }
        if(target!.Assignment.TeamId is not null && !await db.AssignmentCreditedRosters.AnyAsync(r=>r.AssignmentId==job.AssignmentId,ct))
        {
            var team=(await TeamSubmissionAccess.TargetAsync(db,job.AssignmentId))!;
            var members=await TeamSubmissionAccess.EligibleAsync(db,team,DateTimeOffset.UtcNow);
            db.AssignmentCreditedRosters.Add(new() {AssignmentId=job.AssignmentId,TeamId=team.Assignment.TeamId!.Value,ProjectId=team.Assignment.ProjectId!.Value,
                CohortId=team.CohortId,TeamNameSnapshot=team.TeamName,FrozenAt=DateTimeOffset.UtcNow,FrozenBy=job.ActorId});
            foreach(var m in members) db.AssignmentCreditedRosterMembers.Add(new() {AssignmentId=job.AssignmentId,TeamId=team.Assignment.TeamId.Value,
                ProjectId=team.Assignment.ProjectId.Value,CohortId=team.CohortId,UserId=m.UserId,UserNameSnapshot=m.UserName,TeamMembershipId=m.TeamMembershipId,CohortMembershipId=m.CohortMembershipId});
            await db.SaveChangesAsync(ct);
        }
        if(state.Latest?.AttemptNo==int.MaxValue) throw new InvalidOperationException("Attempt number limit reached.");
        var s=new Submission {AssignmentId=job.AssignmentId,AssignmentRevisionId=job.RevisionId,SubmittedBy=job.ActorId,RequestId=job.RequestId,
            Explanation=job.Explanation,ReceivedAt=job.ReceivedAt,RecordedAt=DateTimeOffset.UtcNow,AttemptNo=(state.Latest?.AttemptNo??0)+1,
            CreditedRosterAssignmentId=target.Assignment.TeamId is null ? null : job.AssignmentId};
        db.Submissions.Add(s);
        var links=JsonSerializer.Deserialize<List<SubmissionEvidenceInput>>(job.LinksJson)!;
        for(var i=0;i<links.Count;i++) db.SubmissionEvidence.Add(new() {SubmissionId=s.SubmissionId,SequenceNo=i+1,Label=links[i].Label,Url=links[i].Url});
        job.SubmissionId=s.SubmissionId; job.Status="Committed"; job.Message=$"Submission saved; awaiting review. Attempt {s.AttemptNo}. Receipt: {s.SubmissionId}.";
        await AssignmentWorkflow.TouchAsync(db,job.AssignmentId); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public async Task CleanupAsync(CancellationToken ct=default)
    {
        var now=DateTimeOffset.UtcNow;
        var ids=await db.UploadAttempts.AsNoTracking().Where(a=>a.Status!="Committed" && !a.Hold && (a.LeaseUntil==null || a.LeaseUntil<now)
            && ((a.Status=="Processing" && a.ReceivedAt<now.AddHours(-24)) || ((a.Status=="Rejected" || a.Status=="Expired") && (a.FinishedAt ?? a.ReceivedAt)<now.AddDays(-7))))
            .Select(a=>a.UploadAttemptId).ToListAsync(ct);
        foreach(var id in ids)
        {
            var candidate=await db.UploadAttempts.AsNoTracking().SingleAsync(a=>a.UploadAttemptId==id,ct);
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await AssignmentWorkflow.LockAsync(db,candidate.AssignmentId);
            var a=await db.UploadAttempts.SingleAsync(a=>a.UploadAttemptId==id,ct); await db.Entry(a).ReloadAsync(ct);
            if(a.Status=="Committed" || a.Hold || a.LeaseUntil>now) continue;
            if(a.Status=="Processing") { a.Status="Expired"; a.FinishedAt=now; a.Message="This upload expired without a saved submission. Return to the assignment and upload your files again."; }
            // Failed/expired bytes remain quarantined seven days; committed data never expires here.
            if((a.FinishedAt ?? a.ReceivedAt)<now.AddDays(-7)) foreach(var f in await db.EvidenceFiles.Where(f=>f.UploadAttemptId==id && !f.Deleted).ToListAsync(ct)) { await storage.DeleteAsync(f.ObjectKey,ct); f.Deleted=true; }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        foreach(var (key,created) in storage.OwnedObjects().Where(o=>o.Created<now.AddHours(-24)).ToList())
        {
            // Range lock prevents a receipt from publishing a reference during cleanup.
            // Receive has a 10-minute bound; its 24-hour-old reservation is abandoned.
            await using var orphan=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            if(!await db.EvidenceFiles.AnyAsync(f=>f.ObjectKey==key,ct))
            {
                try { await storage.DeleteAsync(key,ct); }
                catch(IOException) { log.LogWarning("Orphan retained because it is active, held, missing or uncertain."); }
            }
            await orphan.CommitAsync(ct);
        }
        // Objects without a matching durable ownership marker are uncertain and retained.
    }
    internal static async Task<bool> ReviewableAsync(TrackerDbContext db,IPrivateEvidenceStorage storage,Guid submission)
    {
        var id=await db.UploadAttempts.Where(a=>a.SubmissionId==submission && a.Status=="Committed").Select(a=>(Guid?)a.UploadAttemptId).SingleOrDefaultAsync();
        if(id is null) return true; // existing link-only submission
        foreach(var f in await db.EvidenceFiles.AsNoTracking().Where(f=>f.UploadAttemptId==id).ToListAsync())
        { if(f.Deleted || f.ScanResult!="Clean") return false; try { await using var bytes=await storage.OpenVerifiedAsync(f.ObjectKey,f.SizeBytes,f.Sha256,CancellationToken.None); } catch(Exception e) when(e is IOException or UnauthorizedAccessException) { return false; } }
        return true;
    }
}
