using System.Net;
using System.Security.Cryptography;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class FileEvidenceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>, IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"CptEvidenceTests_"+Guid.NewGuid().ToString("N"));
    internal static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC");
    private sealed class Scanner(MalwareResult result) : IMalwareScanner
    { public Task<MalwareResult> ScanAsync(Stream s,CancellationToken ct)=>Task.FromResult(result); }
    private PrivateEvidenceStorage Storage => new(Options.Create(new EvidenceOptions {Root=root,RepositoryRoot=Directory.GetCurrentDirectory()}));
    private FileEvidenceService Service(TrackerDbContext db,MalwareResult scan=MalwareResult.Clean)=>new(db,Storage,new Scanner(scan),NullLogger<FileEvidenceService>.Instance);
    private static FileSubmissionRequest Request(IndividualSubmissionTests.Seed s,Guid? key=null)=>new(new(s.AssignmentId,s.RevisionId,key??Guid.NewGuid(),"Certificate explanation",[]),"",[new("certificate.png",new MemoryStream(Png))]);
    public void Dispose()
    {
        var full=Path.GetFullPath(root); var temp=Path.GetFullPath(Path.GetTempPath());
        if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(full)["CptEvidenceTests_".Length..],"N",out _)) throw new InvalidOperationException("Unexpected temporary storage.");
        if(Directory.Exists(full)) Directory.Delete(full,true);
    }
    private async Task<IndividualSubmissionTests.Seed> SeedAsync(TrackerDbContext db)
    {
        var seed=await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var users=fixture.Services.CreateScope();
        try
        {
            var manager=users.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
            var user=new Microsoft.AspNetCore.Identity.IdentityUser {UserName="file.candidate."+Guid.NewGuid().ToString("N")};
            Assert.True((await manager.CreateAsync(user,"Integration-Only!482")).Succeeded);Assert.True((await manager.AddToRoleAsync(user,"Candidate")).Succeeded);
            var assignment=await db.Assignments.SingleAsync(a=>a.AssignmentId==seed.AssignmentId);assignment.CandidateId=user.Id;
            db.CohortMemberships.Add(new() {CohortId=seed.CohortId,UserId=user.Id,MembershipKind=MembershipKind.Candidate,StartsAt=DateTimeOffset.UtcNow.AddDays(-1),CreatedBy=seed.AdministratorId,ChangeReason="Isolated file candidate"});
            await db.SaveChangesAsync();return seed with {CandidateId=user.Id};
        }
        finally {users.Dispose();}
    }
    [Fact]
    public async Task Course_certificate_is_private_and_uses_shared_review_approval_reopening_and_history()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed=await SeedAsync(db); var courses=new CourseService(db);
        var cycle=await db.Assignments.Where(a=>a.AssignmentId==seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        Assert.NotNull(await courses.CreateAsync("forged","platform","https://example.com",seed.CandidateId));
        var courseTitle="Theory <encoded> "+Guid.NewGuid().ToString("N");
        Assert.Null(await courses.CreateAsync(courseTitle,"Provider","https://example.com/course",seed.AdministratorId));
        var course=await db.Courses.SingleAsync(c=>c.Title==courseTitle); Assert.Null(await courses.OfferAsync(course.CourseId,cycle,seed.AdministratorId));
        var offering=await db.CourseOfferings.SingleAsync(o=>o.CourseId==course.CourseId);
        Assert.Null(await courses.AssignAsync(offering.OfferingId,seed.CandidateId,DateTimeOffset.UtcNow.AddDays(2),"Learn","Pass","Certificate",true,seed.AdministratorId));
        Assert.NotNull(await courses.AssignAsync(offering.OfferingId,seed.CandidateId,DateTimeOffset.UtcNow.AddDays(2),"Learn","Pass","Certificate",true,seed.AdministratorId));
        var a=await db.Assignments.SingleAsync(a=>a.OfferingId==offering.OfferingId);var rev=await db.AssignmentRevisions.SingleAsync(r=>r.AssignmentId==a.AssignmentId);
        var request=Request(seed with {AssignmentId=a.AssignmentId,RevisionId=rev.RevisionId});var files=Service(db);
        var links=await scope.ServiceProvider.GetRequiredService<IIndividualSubmissionService>().SubmitAsync(request.Content with {Evidence=[new("link","https://example.com/cert")]},seed.CandidateId);
        Assert.False(links.Succeeded);Assert.Contains(links.Errors,e=>e.Contains("certificate"));
        var received=await files.ReceiveAsync(request,seed.CandidateId);Assert.Equal("Processing",received.Status);
        var file=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==received.AttemptId);
        Assert.Null(await files.DownloadAsync(file.EvidenceFileId,seed.CandidateId));Assert.Empty(await db.Submissions.Where(s=>s.AssignmentId==a.AssignmentId).ToListAsync());
        Assert.True(await files.ProcessNextAsync());var receipt=await files.GetAsync(received.AttemptId,seed.CandidateId);Assert.Equal("Committed",receipt!.Status);
        var submission=await db.Submissions.SingleAsync(s=>s.SubmissionId==receipt.SubmissionId);Assert.Null(submission.CreditedRosterAssignmentId);
        Assert.Equal((await db.UploadAttempts.FindAsync(received.AttemptId))!.ReceivedAt,submission.ReceivedAt);
        var downloaded=await files.DownloadAsync(file.EvidenceFileId,seed.CandidateId);Assert.NotNull(downloaded);await using(var stream=downloaded.Content) {using var bytes=new MemoryStream();await stream.CopyToAsync(bytes);Assert.Equal(Png,bytes.ToArray());}
        var other=await SeedAsync(db);
        Assert.Null(await files.DownloadAsync(file.EvidenceFileId,other.CandidateId));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IIndividualSubmissionQueryService>().GetHistoryAsync(a.AssignmentId,other.CandidateId));
        var mentor=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where r.NormalizedName=="MENTOR" select ur.UserId).SingleAsync();
        db.CohortMemberships.Add(new() {CohortId=seed.CohortId,UserId=mentor,MembershipKind=MembershipKind.Mentor,StartsAt=DateTimeOffset.UtcNow.AddDays(-1),CreatedBy=seed.AdministratorId,ChangeReason="Course review"});await db.SaveChangesAsync();
        var reviews=new IndividualSubmissionReviewService(db,Storage); var page=(await reviews.GetReviewAsync(a.AssignmentId,mentor))!;Assert.True(page.CanDecide);
        Assert.True((await reviews.DecideAsync(new(a.AssignmentId,submission.SubmissionId,page.RowVersion,Guid.NewGuid(),ReviewDecision.Approved,""),mentor)).Succeeded);
        Assert.Equal("Committed",(await files.ReceiveAsync(Request(seed with {AssignmentId=a.AssignmentId,RevisionId=rev.RevisionId},request.Content.RequestId),seed.CandidateId)).Status);
        Assert.Equal("Rejected",(await files.ReceiveAsync(Request(seed with {AssignmentId=a.AssignmentId,RevisionId=rev.RevisionId}),seed.CandidateId)).Status);
        var reopen=(await reviews.GetReopeningAsync(a.AssignmentId,seed.AdministratorId))!;
        Assert.True((await reviews.ReopenAsync(new(a.AssignmentId,reopen.EffectiveApprovalId!.Value,reopen.RowVersion,Guid.NewGuid(),"Audited reconsideration"),seed.AdministratorId)).Succeeded);
        var history=(await scope.ServiceProvider.GetRequiredService<IIndividualSubmissionQueryService>().GetHistoryAsync(a.AssignmentId,seed.CandidateId))!;
        Assert.Single(history.Attempts);Assert.Single(history.Attempts[0].Files);Assert.NotNull(history.Attempts[0].Reviews.Single().Reopening);
        var archives=new ArchivedHistoryService(db);var active=(await archives.GetAsync(cycle,"Cycle",seed.AdministratorId))!;
        Assert.Null(await archives.ChangeAsync(new(cycle,"Cycle",active.Scope.Version,true,"Close course cycle",Guid.NewGuid()),seed.AdministratorId));
        Assert.Null(await files.DownloadAsync(file.EvidenceFileId,seed.CandidateId));Assert.Null(await files.DownloadAsync(file.EvidenceFileId,mentor));
        var retained=await files.DownloadAsync(file.EvidenceFileId,seed.AdministratorId);Assert.NotNull(retained);await retained.Content.DisposeAsync();
        var archived=(await archives.GetAsync(cycle,"Cycle",seed.AdministratorId))!;
        Assert.Null(await archives.ChangeAsync(new(cycle,"Cycle",archived.Scope.Version,false,"Resume cycle",Guid.NewGuid()),seed.AdministratorId));
        var stored=Path.Combine(root,"cpt-evidence-v1",file.ObjectKey+".object");File.Delete(stored);
        Assert.Null(await files.DownloadAsync(file.EvidenceFileId,seed.CandidateId));Assert.False((await reviews.GetReviewAsync(a.AssignmentId,mentor))!.CanDecide);
    }
    [Theory]
    [InlineData("extension")][InlineData("content")][InlineData("empty")][InlineData("oversize")][InlineData("count")]
    public async Task Invalid_files_leave_no_processing_or_submission_records(string kind)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await SeedAsync(db);
        var data=kind=="oversize" ? new byte[10485761] : kind=="empty" ? [] : kind=="content" ? new byte[]{1,2,3} : Png;
        var input=new EvidenceUpload(kind=="extension" ? "certificate.docx" : "certificate.png",new MemoryStream(data));
        var request=Request(seed) with {Files=kind=="count" ? Enumerable.Range(0,6).Select(_=>input).ToList() : [input]};
        var result=await Service(db).ReceiveAsync(request,seed.CandidateId);Assert.Equal("Rejected",result.Status);
        Assert.False(await db.UploadAttempts.AnyAsync(a=>a.AssignmentId==seed.AssignmentId));Assert.False(await db.Submissions.AnyAsync(s=>s.AssignmentId==seed.AssignmentId));
    }
    [Theory]
    [InlineData("scanner")][InlineData("revision")][InlineData("membership")][InlineData("role")][InlineData("lease")]
    public async Task Durable_finalization_rechecks_scanner_revision_authority_and_recovers_expired_leases(string kind)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await SeedAsync(db);var service=Service(db);
        var receipt=await service.ReceiveAsync(Request(seed),seed.CandidateId);Assert.Equal("Processing",receipt.Status);
        if(kind=="revision") {var old=await db.AssignmentRevisions.SingleAsync(r=>r.RevisionId==seed.RevisionId);db.AssignmentRevisions.Add(new() {AssignmentId=seed.AssignmentId,RevisionNo=2,DueAt=old.DueAt,InstructionsSnapshot=old.InstructionsSnapshot,AcceptanceSnapshot=old.AcceptanceSnapshot,EvidencePolicySnapshot=old.EvidencePolicySnapshot,ChangedAt=DateTimeOffset.UtcNow,ChangedBy=seed.AdministratorId,Reason="Intervening deadline-only revision"});await db.SaveChangesAsync();}
        if(kind=="membership") {var member=await db.CohortMemberships.SingleAsync(m=>m.CohortId==seed.CohortId && m.UserId==seed.CandidateId);member.EndsAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();}
        if(kind=="role") {var users=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(seed.CandidateId))!,"Candidate")).Succeeded);}
        if(kind=="lease") {var a=(await db.UploadAttempts.FindAsync(receipt.AttemptId))!;a.LeaseId=Guid.NewGuid();a.LeaseUntil=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();}
        if(kind=="scanner") service=Service(db,MalwareResult.Unavailable);
        Assert.True(await service.ProcessNextAsync());var stored=(await db.UploadAttempts.FindAsync(receipt.AttemptId))!;
        Assert.Equal(kind=="lease" ? "Committed" : kind=="scanner" ? "Processing" : "Rejected",stored.Status);
        Assert.Equal(kind=="lease" ? 1 : 0,await db.Submissions.CountAsync(s=>s.AssignmentId==seed.AssignmentId));
        if(kind=="scanner") {Assert.Contains("unavailable",stored.Message);stored.NextTryAt=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();Assert.True(await Service(db).ProcessNextAsync());Assert.Equal("Committed",stored.Status);}
    }
    [Fact]
    public async Task Accepted_retries_keep_identity_changed_payload_is_rejected_and_download_http_checks_current_scope()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await SeedAsync(db);
        var options=fixture.Services.GetRequiredService<IOptions<EvidenceOptions>>().Value;options.Root=root;
        var service=Service(db);var request=Request(seed);var first=await service.ReceiveAsync(request,seed.CandidateId);Assert.Equal("Processing",first.Status);
        var retry=await service.ReceiveAsync(Request(seed,request.Content.RequestId),seed.CandidateId);Assert.Equal(first.AttemptId,retry.AttemptId);
        var changed=Request(seed,request.Content.RequestId);changed=changed with {Content=changed.Content with {Explanation="Changed"}};
        Assert.Equal("Rejected",(await service.ReceiveAsync(changed,seed.CandidateId)).Status);
        Assert.True(await service.ProcessNextAsync());Assert.False(await service.ProcessNextAsync());
        Assert.Single(await db.Submissions.Where(s=>s.AssignmentId==seed.AssignmentId).ToListAsync());Assert.Single(await db.EvidenceFiles.Where(f=>f.UploadAttemptId==first.AttemptId).ToListAsync());
        var file=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==first.AttemptId);
        using var owner=await fixture.CreateClientAsync("Candidate",seed.CandidateId);using var response=await owner.GetAsync($"/Evidence/Download/{file.EvidenceFileId}");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("attachment",response.Content.Headers.ContentDisposition!.DispositionType);Assert.Contains("no-store",response.Headers.CacheControl!.ToString());Assert.Equal(Png,await response.Content.ReadAsByteArrayAsync());
        var other=await SeedAsync(db);using var outsider=await fixture.CreateClientAsync("Candidate",other.CandidateId);
        using var denied=await outsider.GetAsync($"/Evidence/Download/{file.EvidenceFileId}");Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        var member=await db.CohortMemberships.SingleAsync(m=>m.CohortId==seed.CohortId && m.UserId==seed.CandidateId);member.EndsAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();
        using var ended=await owner.GetAsync($"/Evidence/Download/{file.EvidenceFileId}");Assert.Equal(HttpStatusCode.NotFound,ended.StatusCode);
    }
    [Fact]
    public async Task Course_admin_navigation_and_multipart_submission_enforce_antiforgery_and_show_processing_then_history()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await SeedAsync(db);
        fixture.Services.GetRequiredService<IOptions<EvidenceOptions>>().Value.Root=root;
        var cycle=await db.Assignments.Where(a=>a.AssignmentId==seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        using var admin=await fixture.CreateClientAsync("Administrator",seed.AdministratorId);
        using var page=await admin.GetAsync($"/Admin/Courses/{cycle}");Assert.Equal(HttpStatusCode.OK,page.StatusCode);SubmissionPageTests.CopyResponseCookies(admin,page);
        var token=SubmissionPageTests.Hidden(await page.Content.ReadAsStringAsync(),"__RequestVerificationToken");var title="Certificate <course> "+Guid.NewGuid().ToString("N");
        async Task Post(Dictionary<string,string> fields)
        { fields["__RequestVerificationToken"]=token;using var result=await admin.PostAsync($"/Admin/Courses/{cycle}",new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.Redirect,result.StatusCode); }
        await Post(new() {["action"]="create",["Input.Title"]=title,["Input.Platform"]="Provider",["Input.Url"]="https://example.com/course"});
        var course=await db.Courses.SingleAsync(c=>c.Title==title);await Post(new() {["action"]="offer",["Input.CourseId"]=course.CourseId.ToString()});
        var offering=await db.CourseOfferings.SingleAsync(o=>o.CourseId==course.CourseId);
        await Post(new() {["action"]="assign",["Input.OfferingId"]=offering.OfferingId.ToString(),["Input.CandidateId"]=seed.CandidateId,
            ["Input.DueLocal"]=DateTimeOffset.UtcNow.AddDays(2).ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-ddTHH:mm"),["Input.Instructions"]="Complete",["Input.Acceptance"]="Certificate",["Input.Evidence"]="Upload certificate",["Input.Required"]="true"});
        var assignment=await db.Assignments.SingleAsync(a=>a.OfferingId==offering.OfferingId);
        using var owner=await fixture.CreateClientAsync("Candidate",seed.CandidateId);var list=await owner.GetStringAsync("/Candidate/Courses");Assert.Contains("&lt;course&gt;",list);
        using var form=await owner.GetAsync($"/Candidate/Submit/{assignment.AssignmentId}");Assert.Equal(HttpStatusCode.OK,form.StatusCode);SubmissionPageTests.CopyResponseCookies(owner,form);
        var html=await form.Content.ReadAsStringAsync();
        MultipartFormDataContent Body(bool csrf)
        {
            var body=new MultipartFormDataContent();
            foreach(var field in new[] {"Input.RevisionId","Input.RequestId"}) body.Add(new StringContent(SubmissionPageTests.Hidden(html,field)),field);
            body.Add(new StringContent("Certificate via protected multipart form"),"Input.Explanation");
            if(csrf) body.Add(new StringContent(SubmissionPageTests.Hidden(html,"__RequestVerificationToken")),"__RequestVerificationToken");
            var bytes=new ByteArrayContent(Png);bytes.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");body.Add(bytes,"Files","certificate.png");return body;
        }
        using(var noToken=await owner.PostAsync($"/Candidate/Submit/{assignment.AssignmentId}",Body(false))) Assert.Equal(HttpStatusCode.BadRequest,noToken.StatusCode);
        Assert.False(await db.UploadAttempts.AnyAsync(a=>a.AssignmentId==assignment.AssignmentId));
        using var posted=await owner.PostAsync($"/Candidate/Submit/{assignment.AssignmentId}",Body(true));Assert.Equal(HttpStatusCode.Redirect,posted.StatusCode);Assert.Contains("/Candidate/Processing/",posted.Headers.Location!.ToString());
        var attempt=await db.UploadAttempts.SingleAsync(a=>a.AssignmentId==assignment.AssignmentId);Assert.Equal("Processing",attempt.Status);
        Assert.Contains("Processing",await owner.GetStringAsync(posted.Headers.Location));Assert.True(await Service(db).ProcessNextAsync());
        var history=await owner.GetStringAsync($"/Candidate/SubmissionHistory/{assignment.AssignmentId}");Assert.Contains("Download certificate.png",history);
    }
    [Fact]
    public async Task Cleanup_preserves_holds_leases_committed_and_unknown_objects_and_expires_abandoned_jobs()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();var seed=await SeedAsync(db);var service=Service(db);
        var receipt=await service.ReceiveAsync(Request(seed),seed.CandidateId);Assert.Equal("Processing",receipt.Status);
        var a=(await db.UploadAttempts.FindAsync(receipt.AttemptId))!;var file=await db.EvidenceFiles.SingleAsync(f=>f.UploadAttemptId==a.UploadAttemptId);a.ReceivedAt=DateTimeOffset.UtcNow.AddDays(-9);a.Hold=true;await db.SaveChangesAsync();
        await service.CleanupAsync();Assert.Equal("Processing",a.Status);Assert.False(file.Deleted);
        a.Hold=false;a.LeaseUntil=DateTimeOffset.UtcNow.AddMinutes(5);await db.SaveChangesAsync();await service.CleanupAsync();Assert.Equal("Processing",a.Status);
        a.LeaseUntil=null;await db.SaveChangesAsync();await service.CleanupAsync();Assert.Equal("Expired",a.Status);Assert.False(file.Deleted);
        a.FinishedAt=DateTimeOffset.UtcNow.AddDays(-8);await db.SaveChangesAsync();await service.CleanupAsync();Assert.True(file.Deleted);
        var orphan=await Storage.StoreAsync("orphan.png",new MemoryStream(Png),CancellationToken.None);
        var orphanPath=Path.Combine(root,"cpt-evidence-v1",orphan.Key+".object");File.SetLastWriteTimeUtc(orphanPath+".staging",DateTime.UtcNow.AddHours(-25));
        var foreignPath=Path.Combine(root,"cpt-evidence-v1",Guid.NewGuid().ToString("N")+".object");await File.WriteAllTextAsync(foreignPath,"Not owned: leave alone");File.SetCreationTimeUtc(foreignPath,DateTime.UtcNow.AddDays(-2));
        await service.CleanupAsync();Assert.False(File.Exists(orphanPath));Assert.True(File.Exists(foreignPath));
        var unknown=await Storage.StoreAsync("unknown.png",new MemoryStream(Png),CancellationToken.None);await service.CleanupAsync();await using var bytes=await Storage.OpenVerifiedAsync(unknown.Key,unknown.Length,unknown.Hash,CancellationToken.None);Assert.True(bytes.Length>0);
    }
}
