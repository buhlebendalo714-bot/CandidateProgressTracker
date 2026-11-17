using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class TeamReviewTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Approval_reopening_cycles_keep_roster_history_receipts_and_require_new_attempt_after_changes_requested()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var original = await t.RequestAsync(); var first = await t.Teams.SubmitAsync(original, t.Seed.CandidateId);
        var staleDecision = await t.DecisionAsync();
        for (var cycle = 0; cycle < 2; cycle++)
        {
            var decision = await t.DecisionAsync();
            var approved = await t.Reviews.DecideAsync(decision, t.MentorId); Assert.True(approved.Succeeded);
            Assert.Equal(approved.ReceiptId, (await t.Reviews.DecideAsync(decision, t.MentorId)).ReceiptId);
            Assert.False((await t.Reviews.DecideAsync(decision with { Feedback = "Changed retry" }, t.MentorId)).Succeeded);
            Assert.False((await t.Teams.SubmitAsync(await t.RequestAsync(), t.OtherId)).Succeeded);
            Assert.Equal(first.Receipt, (await t.Teams.SubmitAsync(original, t.Seed.CandidateId)).Receipt);
            var reopening = await t.ReopeningAsync();
            var opened = await t.Reopenings.ReopenAsync(reopening, t.Seed.AdministratorId); Assert.True(opened.Succeeded);
            Assert.Equal(opened.ReceiptId, (await t.Reopenings.ReopenAsync(reopening, t.Seed.AdministratorId)).ReceiptId);
            Assert.False((await t.Reopenings.ReopenAsync(reopening with { Reason = "Changed retry" }, t.Seed.AdministratorId)).Succeeded);
            Assert.False((await t.Reopenings.ReopenAsync(reopening with { RequestId = Guid.NewGuid() }, t.Seed.AdministratorId)).Succeeded);
        }
        Assert.False((await t.Reviews.DecideAsync(staleDecision, t.MentorId)).Succeeded);
        Assert.False((await t.Reviews.DecideAsync(await t.DecisionAsync(ReviewDecision.ChangesRequested, "  "), t.MentorId)).Succeeded);
        Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(ReviewDecision.ChangesRequested, "Please improve <evidence>"), t.MentorId)).Succeeded);
        Assert.False((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        Assert.False((await t.Reopenings.ReopenAsync(await t.ReopeningAsync(), t.Seed.AdministratorId)).Succeeded);
        await t.SubmitAsync(t.OtherId);
        Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        var history = (await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, t.Seed.CandidateId))!;
        Assert.Equal(2, history.Roster!.Members.Count);
        Assert.Equal(2, history.Attempts.Count);
        Assert.Equal(3, history.Attempts.Single(a => a.AttemptNo == 1).Reviews.Count);
        Assert.Equal(2, await t.Db.AssignmentReopenings.CountAsync(r => r.AssignmentId == t.Seed.AssignmentId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deadline_changes_preserve_approval_and_eligible_review_but_requirement_change_and_revert_permanently_invalidate(bool approveFirst)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var receipt = await t.SubmitAsync();
        if (approveFirst) Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        var before = (await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, t.Seed.CandidateId))!.Attempts.Single();
        await t.ReviseAsync();
        if (approveFirst)
            Assert.NotNull((await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId))!.EffectiveApprovalId);
        else
            Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        var after = (await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, t.Seed.CandidateId))!.Attempts.Single();
        Assert.Equal(before.RevisionId, after.RevisionId); Assert.Equal(before.DueAt, after.DueAt); Assert.Equal(before.IsLate, after.IsLate);
        await t.ReviseAsync(requirements: true); await t.ReviseAsync(revert: true);
        var page = (await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId))!;
        Assert.Null(page.EffectiveApprovalId); Assert.False(page.CanDecide);
        Assert.False((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        Assert.False((await t.Reopenings.ReopenAsync(await t.ReopeningAsync(), t.Seed.AdministratorId)).Succeeded);
        var fresh = await t.SubmitAsync(t.OtherId); Assert.Equal(2, fresh.AttemptNo);
        Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        Assert.Equal(2, await t.Db.AssignmentCreditedRosterMembers.CountAsync(m => m.AssignmentId == t.Seed.AssignmentId));
        Assert.Equal(t.Seed.RevisionId, (await t.Db.Submissions.AsNoTracking().SingleAsync(s => s.SubmissionId == receipt.SubmissionId)).AssignmentRevisionId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Credited_dual_role_members_cannot_review_even_after_leaving_whether_or_not_they_submitted(bool actualSubmitter)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture); await t.SubmitAsync();
        var actor = actualSubmitter ? t.Seed.CandidateId : t.OtherId;
        var users = t.Services.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(actor))!, "Mentor")).Succeeded);
        t.Db.CohortMemberships.Add(new() { CohortId = t.Seed.CohortId, UserId = actor,
            MembershipKind = CandidateProgressTracker.Domain.Cohorts.MembershipKind.Mentor, StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedBy = t.Seed.AdministratorId, ChangeReason = "Dual role" }); await t.Db.SaveChangesAsync();
        await t.EndTeamAsync(actor);
        var page = (await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, actor))!;
        Assert.False(page.CanDecide);
        Assert.False((await t.Reviews.DecideAsync((await t.DecisionAsync()) with { RowVersion = page.RowVersion }, actor)).Succeeded);
        Assert.Empty(await t.Db.SubmissionReviews.Where(r => r.AssignmentId == t.Seed.AssignmentId).ToListAsync());
    }

    [Fact]
    public async Task Mentor_can_review_when_candidates_lose_membership_but_administrator_has_no_review_power()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture); await t.SubmitAsync();
        foreach (var member in await t.Db.CohortMemberships.Where(m => m.CohortId == t.Seed.CohortId && m.MembershipKind == CandidateProgressTracker.Domain.Cohorts.MembershipKind.Candidate).ToListAsync())
            member.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await t.Db.SaveChangesAsync();
        Assert.Null(await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, t.Seed.CandidateId));
        Assert.NotNull(await t.Teams.GetMentorHistoryAsync(t.Seed.AssignmentId, t.MentorId));
        Assert.False((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.Seed.AdministratorId)).Succeeded);
        Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
    }

    [Theory]
    [InlineData("ended")]
    [InlineData("future")]
    [InlineData("role")]
    [InlineData("outside")]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("cohort")]
    public async Task Team_review_rechecks_mentor_scope_and_archival_after_opening_form(string denial)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture); await t.SubmitAsync();
        var request = await t.DecisionAsync(); var mentor = t.MentorId;
        switch (denial)
        {
            case "ended":
            case "future":
                var m = await t.Db.CohortMemberships.SingleAsync(m => m.UserId == mentor && m.CohortId == t.Seed.CohortId);
                if (denial == "ended") m.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1); else m.StartsAt = DateTimeOffset.UtcNow.AddDays(1);
                break;
            case "role":
                var users = t.Services.GetRequiredService<UserManager<IdentityUser>>();
                Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(mentor))!, "Mentor")).Succeeded); break;
            case "outside":
                var original = await t.Db.CohortMemberships.SingleAsync(m => m.UserId == mentor && m.CohortId == t.Seed.CohortId);
                // A current Mentor elsewhere does not gain access to this cohort.
                var elsewhere = await t.Db.Cohorts.Where(c => c.CohortId != t.Seed.CohortId).Select(c => c.CohortId).FirstAsync();
                original.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                t.Db.CohortMemberships.Add(new() { CohortId = elsewhere, UserId = mentor,
                    MembershipKind = CandidateProgressTracker.Domain.Cohorts.MembershipKind.Mentor, StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                    CreatedBy = t.Seed.AdministratorId, ChangeReason = "Elsewhere" }); break;
            case "project":
                var p = await t.Db.Projects.SingleAsync(p => p.ProjectId == t.Seed.ProjectId);
                p.IsArchived = true; p.ArchivedAt = DateTimeOffset.UtcNow; p.ArchivedBy = t.Seed.AdministratorId; p.ArchiveReason = "Test"; break;
            case "cycle":
                var cycleId = await t.Db.Projects.Where(p => p.ProjectId == t.Seed.ProjectId).Select(p => p.CycleId).SingleAsync();
                var c = await t.Db.LearningCycles.SingleAsync(c => c.CycleId == cycleId);
                c.IsArchived = true; c.ArchivedAt = DateTimeOffset.UtcNow; c.ArchivedBy = t.Seed.AdministratorId; c.ArchiveReason = "Test"; break;
            case "cohort":
                var cohort = await t.Db.Cohorts.SingleAsync(c => c.CohortId == t.Seed.CohortId);
                cohort.IsArchived = true; cohort.ArchivedAt = DateTimeOffset.UtcNow; cohort.ArchivedBy = t.Seed.AdministratorId; cohort.ArchiveReason = "Test"; break;
        }
        await t.Db.SaveChangesAsync();
        Assert.Null(await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, mentor));
        Assert.Null(await t.Teams.GetMentorHistoryAsync(t.Seed.AssignmentId, mentor));
        Assert.False((await t.Reviews.DecideAsync(request, mentor)).Succeeded);
        Assert.False(await t.Db.SubmissionReviews.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        using var client = await fixture.CreateClientAsync(denial == "role" ? "Candidate" : "Mentor", mentor);
        using var response = await client.GetAsync($"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}");
        Assert.Equal(denial == "role" ? System.Net.HttpStatusCode.Redirect : System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
