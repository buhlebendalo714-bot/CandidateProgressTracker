using System.Net;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class LearningOverviewTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Effective_approval_counts_separately_reopening_and_requirement_revert_invalidate_and_pending_is_not_overdue()
    {
        await using var t=await ReviewTestContext.CreateAsync(fixture);var service=new LearningOverviewService(t.Db);var cycle=await t.Db.Assignments.Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        var course=new CourseService(t.Db);var title="Progress theory "+Guid.NewGuid().ToString("N");Assert.Null(await course.CreateAsync(title,"Provider","https://example.com/course",t.Seed.AdministratorId));var c=await t.Db.Courses.SingleAsync(c=>c.Title==title);
        Assert.Null(await course.OfferAsync(c.CourseId,cycle,t.Seed.AdministratorId));var o=await t.Db.CourseOfferings.SingleAsync(o=>o.CourseId==c.CourseId);
        Assert.Null(await course.AssignAsync(o.OfferingId,t.Seed.CandidateId,DateTimeOffset.UtcNow.AddDays(1),"Course","Certificate","Certificate",true,t.Seed.AdministratorId));
        var a=await t.Db.Assignments.SingleAsync(a=>a.AssignmentId==t.Seed.AssignmentId);a.DueAt=DateTimeOffset.UtcNow.AddDays(-1);await t.Db.SaveChangesAsync();
        var pending=(await service.GetCandidateAsync(t.Seed.CandidateId,cycle))!;Assert.Equal(1,pending.Projects.Required);Assert.Equal(0,pending.Projects.Overdue);Assert.Equal(1,pending.Projects.Pending);Assert.Equal(1,pending.Theory.Required);Assert.Equal(0,pending.Theory.Approved);
        await t.ApproveAsync();var approved=(await service.GetCandidateAsync(t.Seed.CandidateId,cycle))!;Assert.Equal(100m,approved.Projects.Percentage);Assert.Equal(0m,approved.Theory.Percentage);
        await t.ReviseAsync(deadlineOnly:true);Assert.Equal(1,(await service.GetCandidateAsync(t.Seed.CandidateId,cycle))!.Projects.Approved);
        Assert.True((await t.Reopenings.ReopenAsync(await t.ReopeningAsync(),t.Seed.AdministratorId)).Succeeded);Assert.Equal(0,(await service.GetCandidateAsync(t.Seed.CandidateId,cycle))!.Projects.Approved);
        await t.ApproveAsync();await t.ReviseAsync(instructions:"Changed");await t.ReviseAsync(instructions:"Complete the work.");
        var changed=(await service.GetCandidateAsync(t.Seed.CandidateId,cycle))!;Assert.Equal(0,changed.Projects.Approved);Assert.Contains(changed.Assignments,r=>r.Status=="Requirements changed");
    }
    [Fact]
    public async Task Team_totals_follow_current_access_and_frozen_credit_joiners_read_without_counts_and_leavers_are_excluded()
    {
        await using var t=await TeamTestContext.CreateAsync(fixture);var service=new LearningOverviewService(t.Db);var cycle=await t.Db.Assignments.Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        Assert.Equal(1,(await service.GetCandidateAsync(t.OtherId,cycle))!.Projects.Required);await t.SubmitAsync();
        Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(),t.MentorId)).Succeeded);
        var joiner=await t.AddUserAsync();var newer=(await service.GetCandidateAsync(joiner,cycle))!;Assert.Equal(0,newer.Projects.Required);Assert.Null(newer.Projects.Percentage);Assert.False(newer.Assignments.Single().CountsForProgress);Assert.False(newer.Assignments.Single().CanSubmit);
        Assert.Equal(1,(await service.GetCandidateAsync(t.OtherId,cycle))!.Projects.Approved);await t.EndTeamAsync(t.OtherId);var leaver=(await service.GetCandidateAsync(t.OtherId,cycle))!;
        Assert.Empty(leaver.Assignments);Assert.Equal(0,leaver.Projects.Required);Assert.Equal(2,await t.Db.AssignmentCreditedRosterMembers.CountAsync(m=>m.AssignmentId==t.Seed.AssignmentId));
        var users=t.Services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
        Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(t.MentorId))!,"Mentor")).Succeeded);
        Assert.Null(await service.GetMentorAsync(t.MentorId,cycle));Assert.Null(await service.GetQueueAsync(t.MentorId,new(CycleId:cycle)));
    }
    [Fact]
    public async Task Overview_scope_denies_other_cohort_wrong_roles_ended_memberships_and_archived_cycles_in_services_and_HTTP()
    {
        await using var t=await ReviewTestContext.CreateAsync(fixture);var service=new LearningOverviewService(t.Db);var cycle=await t.Db.Assignments.Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        Assert.Null(await service.GetCandidateAsync(t.Seed.CandidateId,Guid.NewGuid()));Assert.Null(await service.GetMentorAsync(t.Seed.CandidateId,cycle));
        using var candidate=await fixture.CreateClientAsync("Candidate",t.Seed.CandidateId);using var denied=await candidate.GetAsync("/Mentor/ReviewQueue");Assert.Equal(HttpStatusCode.Redirect,denied.StatusCode);
        Assert.NotNull(await service.GetMentorAsync(t.MentorId,cycle));var m=await t.Db.CohortMemberships.SingleAsync(m=>m.UserId==t.MentorId && m.CohortId==t.Seed.CohortId);m.EndsAt=DateTimeOffset.UtcNow;await t.Db.SaveChangesAsync();Assert.Null(await service.GetMentorAsync(t.MentorId,cycle));
        var parent=await t.Db.LearningCycles.SingleAsync(c=>c.CycleId==cycle);parent.IsArchived=true;parent.ArchivedAt=DateTimeOffset.UtcNow;parent.ArchivedBy=t.Seed.AdministratorId;parent.ArchiveReason="Scope test";await t.Db.SaveChangesAsync();Assert.Null(await service.GetCandidateAsync(t.Seed.CandidateId,cycle));
        using var archived=await candidate.GetAsync($"/Candidate/Progress?cycleId={cycle}");Assert.Equal(HttpStatusCode.NotFound,archived.StatusCode);
    }
    [Fact]
    public async Task Queue_uses_only_latest_committed_attempt_oldest_first_twenty_items_filters_and_mentor_scope_after_candidate_membership_ends()
    {
        await using var t=await ReviewTestContext.CreateAsync(fixture);var db=t.Db;var service=new LearningOverviewService(db);var cycle=await db.Assignments.Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        var original=(await db.Assignments.AsNoTracking().SingleAsync(a=>a.AssignmentId==t.Seed.AssignmentId));
        for(var i=0;i<20;i++)
        {
            var milestone=new Milestone {ProjectId=t.Seed.ProjectId,SequenceNo=i+2,Title="Queue "+i,Instructions="Work",AcceptanceExpectations="Evidence",EvidencePolicy="Link",CreatedBy=t.Seed.AdministratorId,CreatedAt=DateTimeOffset.UtcNow};
            var a=new Assignment {CycleId=cycle,ProjectId=t.Seed.ProjectId,MilestoneId=milestone.MilestoneId,CandidateId=t.Seed.CandidateId,DueAt=original.DueAt,CreatedBy=t.Seed.AdministratorId,CreatedAt=DateTimeOffset.UtcNow};
            var r=new AssignmentRevision {AssignmentId=a.AssignmentId,RevisionNo=1,DueAt=a.DueAt,InstructionsSnapshot="Work",AcceptanceSnapshot="Evidence",EvidencePolicySnapshot="Link",ChangedBy=t.Seed.AdministratorId,ChangedAt=DateTimeOffset.UtcNow,Reason="Queue test"};
            db.AddRange(milestone,a,r);db.Submissions.Add(new() {AssignmentId=a.AssignmentId,AssignmentRevisionId=r.RevisionId,AttemptNo=1,RequestId=Guid.NewGuid(),Explanation="Queue evidence",SubmittedBy=t.Seed.CandidateId,ReceivedAt=DateTimeOffset.UtcNow.AddMinutes(-i-2),RecordedAt=DateTimeOffset.UtcNow});
        }
        await db.SaveChangesAsync();var member=await db.CohortMemberships.SingleAsync(m=>m.CohortId==t.Seed.CohortId && m.UserId==t.Seed.CandidateId);member.EndsAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();
        var first=(await service.GetQueueAsync(t.MentorId,new(CycleId:cycle)))!;Assert.Equal(20,first.Rows.Count);Assert.True(first.HasNextPage);Assert.Equal(first.Rows.OrderBy(r=>r.ReceivedAt).Select(r=>r.AssignmentId),first.Rows.Select(r=>r.AssignmentId));
        var second=(await service.GetQueueAsync(t.MentorId,new(CycleId:cycle,Page:2)))!;Assert.Single(second.Rows);Assert.False(second.HasNextPage);Assert.DoesNotContain(second.Rows[0].AssignmentId,first.Rows.Select(r=>r.AssignmentId));
        Assert.Empty((await service.GetQueueAsync(t.MentorId,new(CycleId:cycle,Status:"Approved")))!.Rows);Assert.Null(await service.GetQueueAsync(t.MentorId,new(CohortId:Guid.NewGuid())));
        using var mentor=await fixture.CreateClientAsync("Mentor",t.MentorId);using var page=await mentor.GetAsync($"/Mentor/ReviewQueue?cycleId={cycle}");Assert.Equal(HttpStatusCode.OK,page.StatusCode);Assert.Contains("Next",await page.Content.ReadAsStringAsync());
    }
}
