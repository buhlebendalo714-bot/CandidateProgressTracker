using System.Net;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class ArchiveAuditTests(WebAccessFixture fixture):IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Project_archive_and_reopen_are_reasoned_versioned_idempotent_and_preserve_read_only_admin_history_and_outcomes()
    {
        await using var t=await ReviewTestContext.CreateAsync(fixture);await t.ApproveAsync();var service=t.Services.GetRequiredService<IArchivedHistoryService>();
        var details=(await service.GetAsync(t.Seed.ProjectId,"Project",t.Seed.AdministratorId))!;
        var request=new ArchiveRequest(t.Seed.ProjectId,"Project",details.Scope.Version,true,"Work closed",Guid.NewGuid());
        Assert.Null(await service.ChangeAsync(request,t.Seed.AdministratorId));Assert.Null(await service.ChangeAsync(request,t.Seed.AdministratorId));
        Assert.NotNull(await service.ChangeAsync(request with{Reason="Changed reuse"},t.Seed.AdministratorId));
        Assert.Null(await t.Services.GetRequiredService<IIndividualSubmissionQueryService>().GetHistoryAsync(t.Seed.AssignmentId,t.Seed.CandidateId));Assert.Null(await t.Reviews.GetReviewAsync(t.Seed.AssignmentId,t.MentorId));
        var history=(await service.HistoryAsync(t.Seed.AssignmentId,t.Seed.AdministratorId))!;Assert.Single(history.Attempts);Assert.Single(history.Attempts[0].Reviews);
        Assert.Null(await service.HistoryAsync(t.Seed.AssignmentId,t.Seed.CandidateId));Assert.Null(await service.GetScopesAsync(t.MentorId));
        var archived=(await service.GetAsync(t.Seed.ProjectId,"Project",t.Seed.AdministratorId))!;Assert.Single(archived.Events);Assert.Contains(archived.Events[0].Outcomes,o=>o.ReceiptId==t.OriginalReceipt.SubmissionId&&o.Approved);
        Assert.NotNull(await service.ChangeAsync(request with{RequestId=Guid.NewGuid(),Archive=false},t.Seed.AdministratorId));
        Assert.Null(await service.ChangeAsync(request with{RequestId=Guid.NewGuid(),Version=archived.Scope.Version,Archive=false,Reason="Reopen investigation"},t.Seed.AdministratorId));
        Assert.NotNull(await t.Services.GetRequiredService<IIndividualSubmissionQueryService>().GetHistoryAsync(t.Seed.AssignmentId,t.Seed.CandidateId));
        Assert.False((await t.Submissions.SubmitAsync(await t.NewRequestAsync(),t.Seed.CandidateId)).Succeeded); // Archive reopening never reopens approval.
        Assert.Equal(2,await t.Db.ScopeArchiveEvents.CountAsync(e=>e.ScopeId==t.Seed.ProjectId));
        using var admin=await fixture.CreateClientAsync("Administrator",t.Seed.AdministratorId);Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync($"/Admin/SubmissionHistory?assignmentId={t.Seed.AssignmentId}")).StatusCode);
        using var candidate=await fixture.CreateClientAsync("Candidate",t.Seed.CandidateId);Assert.Equal(HttpStatusCode.Redirect,(await candidate.GetAsync("/Admin/Archives")).StatusCode);
    }
    [Fact]
    public async Task Audit_is_searchable_scoped_append_only_and_rolls_back_with_the_business_transaction()
    {
        await using var t=await ReviewTestContext.CreateAsync(fixture);var audit=t.Services.GetRequiredService<IAuditQueryService>();
        var rows=(await audit.SearchAsync(new(Record:t.OriginalReceipt.SubmissionId.ToString()),t.Seed.AdministratorId))!;Assert.Single(rows.Rows);Assert.Equal("Submission",rows.Rows[0].EntityType);
        Assert.Null(await audit.SearchAsync(new(),t.Seed.CandidateId));
        var existing=await t.Db.AuditEvents.CountAsync();
        await using(var tx=await t.Db.Database.BeginTransactionAsync())
        {var p=await t.Db.Projects.SingleAsync(p=>p.ProjectId==t.Seed.ProjectId);p.Title="Rolled back";t.Db.AuditActorId=t.Seed.AdministratorId;await t.Db.SaveChangesAsync();Assert.True(await t.Db.AuditEvents.CountAsync()>existing);await tx.RollbackAsync();}
        t.Db.ChangeTracker.Clear();Assert.Equal(existing,await t.Db.AuditEvents.CountAsync());Assert.NotEqual("Rolled back",(await t.Db.Projects.SingleAsync(p=>p.ProjectId==t.Seed.ProjectId)).Title);
        var id=rows.Rows[0].Id;await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>t.Db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM AuditEvents WHERE AuditEventId={id}"));
        using var admin=await fixture.CreateClientAsync("Administrator",t.Seed.AdministratorId);Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync($"/Admin/Audit?Record={t.OriginalReceipt.SubmissionId}")).StatusCode);
    }
}
