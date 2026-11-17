using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class CommunicationAcceptanceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Recovery_bulk_errors_team_processing_and_audit_explain_outcome_without_leaking_codes()
    {
        await using var t=await ReviewTestContext.CreateAsync(fixture);
        await using var team=await TeamTestContext.CreateAsync(fixture);
        using var admin=await fixture.CreateClientAsync("Administrator",t.Seed.AdministratorId);
        var manage="/Admin/Users/Manage?UserId="+t.Seed.CandidateId;
        var opened=await admin.GetAsync(manage);SubmissionPageTests.CopyResponseCookies(admin,opened);
        var fields=new Dictionary<string,string>{["__RequestVerificationToken"]=SubmissionPageTests.Hidden(await opened.Content.ReadAsStringAsync(),"__RequestVerificationToken"),
            ["UserId"]=t.Seed.CandidateId,["Version"]=SubmissionPageTests.Hidden(await opened.Content.ReadAsStringAsync(),"Version"),["Reason"]="Disposable communication review",["Operation"]="Recovery"};
        var response=await admin.PostAsync(manage,new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var html=await response.Content.ReadAsStringAsync();
        Assert.Contains("Password reset code created",html);Assert.Contains("shown only on this page",html);Assert.Contains("Previous codes no longer work",html);
        var redacted=Regex.Replace(html,"<code>.*?</code>","<code>[code removed before browser review]</code>",RegexOptions.Singleline);
        await Browser(admin,manage,redacted,"recovery");
        var bulk="/Admin/Cohorts/BulkOnboard/"+t.Seed.CohortId;
        var bulkGet=await admin.GetAsync(bulk);SubmissionPageTests.CopyResponseCookies(admin,bulkGet);
        using var upload=new MultipartFormDataContent {
            {new StringContent(SubmissionPageTests.Hidden(await bulkGet.Content.ReadAsStringAsync(),"__RequestVerificationToken")),"__RequestVerificationToken"},
            {new StringContent(DateTime.UtcNow.ToString("yyyy-MM-dd")),"StartDate"},
            {new ByteArrayContent([]),"CsvFile","empty.csv"}};
        var rejected=await admin.PostAsync(bulk,upload);Assert.Equal(HttpStatusCode.OK,rejected.StatusCode);
        var invalid=await rejected.Content.ReadAsStringAsync();Assert.DoesNotContain("Row 0",invalid);Assert.Contains("Choose a CSV file with at least one candidate",invalid);
        await Browser(admin,bulk,invalid,"bulk-empty");
        using var candidate=await fixture.CreateClientAsync("Candidate",team.Seed.CandidateId);
        var teamPath="/Candidate/TeamSubmit/"+team.Seed.AssignmentId;var teamHtml=await candidate.GetStringAsync(teamPath);
        Assert.Contains("Confirm members and submit",teamHtml);Assert.Contains("does not prove individual contribution",teamHtml);Assert.Contains("10 MB each",teamHtml);
        await Browser(candidate,teamPath,teamHtml,"team");
        // A disposable durable pending upload exercises the real authorised status page.
        var attempt=new UploadAttempt {AssignmentId=team.Seed.AssignmentId,RevisionId=team.Seed.RevisionId,ActorId=team.Seed.CandidateId,
            RequestId=Guid.NewGuid(),PayloadHash=new string('0',64),ReceivedAt=DateTimeOffset.UtcNow,NextTryAt=DateTimeOffset.UtcNow,
            ExpectedVersion=(await team.Db.Assignments.SingleAsync(a=>a.AssignmentId==team.Seed.AssignmentId)).RowVersion};
        team.Db.UploadAttempts.Add(attempt);await team.Db.SaveChangesAsync();
        var processing="/Candidate/Processing/"+attempt.UploadAttemptId;var processingHtml=await candidate.GetStringAsync(processing);
        Assert.Contains("Checking uploaded files",processingHtml);Assert.DoesNotContain("Submission saved.",processingHtml);
        await Browser(candidate,processing,processingHtml,"processing");
        var audit=await t.Services.GetRequiredService<IAuditQueryService>().SearchAsync(new(Record:"AccountCredential"),t.Seed.AdministratorId);
        Assert.NotNull(audit);Assert.Contains(audit.Rows,e=>e.DisplayAction=="Password reset code created"&&e.ActorName is not null);
        var auditPath="/Admin/Audit?Record=AccountCredential";var auditHtml=await admin.GetStringAsync(auditPath);
        Assert.Contains("Password reset code created",auditHtml);Assert.Contains("Record references and source",auditHtml);
        Assert.True(!Regex.IsMatch(auditHtml,"<code>"),"Audit must not contain any code panel");
        await Browser(admin,auditPath,auditHtml,"audit");
    }
    private static Task Browser(HttpClient client,string path,string html,string label)=>
        Environment.GetEnvironmentVariable("CPT_COMMUNICATION_BROWSER")=="1"
        ?CandidateDashboardTests.VerifyBrowserAsync(client,path,html,"communication-review.cjs","communication/"+label)
        :Task.CompletedTask;
}
