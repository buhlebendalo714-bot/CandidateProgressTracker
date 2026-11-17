using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class SubmissionReviewTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deadline_only_revision_preserves_approval_and_exact_submitted_revision(bool reviseBeforeReview)
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var original = (await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId))!.Attempt!;
        if (reviseBeforeReview) await t.ReviseAsync(deadlineOnly: true);
        var approved = await t.ApproveAsync();
        if (!reviseBeforeReview) await t.ReviseAsync(deadlineOnly: true);
        var page = (await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId))!;
        Assert.Equal(approved.ReceiptId, page.EffectiveApprovalId);
        Assert.Equal(original.RevisionId, page.Attempt!.RevisionId);
        Assert.Equal(original.DueAt, page.Attempt.DueAt);
        Assert.Equal(original.IsLate, page.Attempt.IsLate);
        Assert.Equal("Effective approval", Assert.Single(page.Attempt.Reviews).Status);
        var blocked = await t.Submissions.SubmitAsync(await t.NewRequestAsync(), t.Seed.CandidateId);
        Assert.False(blocked.Succeeded);
        Assert.Contains("This assignment has an effective approval. New submissions are blocked.", blocked.Errors);
        var retry = await t.Submissions.SubmitAsync(t.OriginalRequest, t.Seed.CandidateId);
        Assert.Equal(t.OriginalReceipt, retry.Receipt);
    }

    [Theory]
    [InlineData("instructions", false)]
    [InlineData("instructions", true)]
    [InlineData("acceptance", false)]
    [InlineData("acceptance", true)]
    [InlineData("evidence", false)]
    [InlineData("evidence", true)]
    [InlineData("case", true)]
    public async Task Every_intervening_requirement_change_permanently_invalidates_approval(string field, bool revert)
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var original = (await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId))!.Attempt!;
        var approval = await t.ApproveAsync();
        await t.ReviseAsync(instructions: field == "instructions" ? "Changed instructions" : field == "case" ? original.Instructions.ToUpperInvariant() : null,
            acceptance: field == "acceptance" ? "Changed acceptance" : null,
            evidence: field == "evidence" ? "Changed evidence" : null);
        if (revert) await t.ReviseAsync(original.Instructions, original.AcceptanceExpectations, original.EvidencePolicy);
        var page = (await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId))!;
        Assert.Null(page.EffectiveApprovalId);
        Assert.False(page.CanDecide);
        Assert.Contains("requirements changed", page.Attempt!.Reviews.Single().Status);
        Assert.Equal(original.RevisionId, page.Attempt.RevisionId);
        Assert.False((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        var reopening = await t.ReopeningAsync();
        Assert.False((await t.Reopenings.ReopenAsync(reopening with { ApprovalReviewId = approval.ReceiptId!.Value }, t.Seed.AdministratorId)).Succeeded);
        var newAttempt = await t.Submissions.SubmitAsync(await t.NewRequestAsync(), t.Seed.CandidateId);
        Assert.True(newAttempt.Succeeded, string.Join("; ", newAttempt.Errors));
        Assert.True((await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId))!.CanDecide);
        await t.ApproveAsync();
    }

    [Fact]
    public async Task Reopen_cycles_are_audited_and_changes_requested_always_requires_another_attempt()
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        for (var cycle = 0; cycle < 2; cycle++)
        {
            var decision = await t.DecisionAsync();
            var approved = await t.Reviews.DecideAsync(decision, t.MentorId);
            Assert.True(approved.Succeeded);
            var request = await t.ReopeningAsync();
            var reopened = await t.Reopenings.ReopenAsync(request, t.Seed.AdministratorId);
            Assert.True(reopened.Succeeded, string.Join("; ", reopened.Errors));
            Assert.Equal(reopened.ReceiptId, (await t.Reopenings.ReopenAsync(request, t.Seed.AdministratorId)).ReceiptId);
            Assert.Equal(approved.ReceiptId, (await t.Reviews.DecideAsync(decision, t.MentorId)).ReceiptId);
            Assert.False((await t.Reviews.DecideAsync(decision with { Feedback = "Changed" }, t.MentorId)).Succeeded);
            Assert.False((await t.Reopenings.ReopenAsync(request with { Reason = "Changed" }, t.Seed.AdministratorId)).Succeeded);
        }
        var changes = await t.DecisionAsync(ReviewDecision.ChangesRequested, "<b>Please add evidence.</b>");
        Assert.True((await t.Reviews.DecideAsync(changes, t.MentorId)).Succeeded);
        Assert.False((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        Assert.False((await t.Reopenings.ReopenAsync(await t.ReopeningAsync(), t.Seed.AdministratorId)).Succeeded);
        Assert.True((await t.Submissions.SubmitAsync(await t.NewRequestAsync(), t.Seed.CandidateId)).Succeeded);
        await t.ApproveAsync();
        var reviews = await t.Db.SubmissionReviews.Where(r => r.AssignmentId == t.Seed.AssignmentId).OrderBy(r => r.ReviewNo).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3, 4 }, reviews.Select(r => r.ReviewNo));
        Assert.Equal(2, await t.Db.AssignmentReopenings.CountAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        var history = (await t.Services.GetRequiredService<IIndividualSubmissionQueryService>().GetHistoryAsync(t.Seed.AssignmentId, t.Seed.CandidateId))!;
        Assert.Equal(2, history.Attempts.Count);
        Assert.Equal(3, history.Attempts.Single(a => a.AttemptNo == 1).Reviews.Count);
        Assert.All(history.Attempts.Single(a => a.AttemptNo == 1).Reviews.Where(r => r.Decision == ReviewDecision.Approved), r => Assert.NotNull(r.Reopening));
    }

    [Theory]
    [InlineData("blank-feedback")]
    [InlineData("long-feedback")]
    [InlineData("invalid-decision")]
    [InlineData("empty-request")]
    public async Task Invalid_decision_does_not_write(string scenario)
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var request = await t.DecisionAsync(ReviewDecision.ChangesRequested, "Valid feedback");
        request = scenario switch
        {
            "blank-feedback" => request with { Feedback = " \t\n" },
            "long-feedback" => request with { Feedback = new string('x', ReviewLimits.Feedback + 1) },
            "invalid-decision" => request with { Decision = (ReviewDecision)99 },
            _ => request with { RequestId = Guid.Empty }
        };
        Assert.False((await t.Reviews.DecideAsync(request, t.MentorId)).Succeeded);
        Assert.False(await t.Db.SubmissionReviews.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
    }

    [Theory]
    [InlineData("mentor-role")]
    [InlineData("ended")]
    [InlineData("future")]
    [InlineData("other-cohort")]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("cohort")]
    [InlineData("team")]
    [InlineData("candidate-ended")]
    [InlineData("self")]
    public async Task Decision_rechecks_authorization_after_form_open(string scenario)
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var request = await t.DecisionAsync();
        var actor = t.MentorId;
        var roles = await t.Db.UserRoles.Where(r => r.UserId == t.MentorId).ToListAsync();
        var mentor = await t.Db.CohortMemberships.SingleAsync(m => m.CohortId == t.Seed.CohortId && m.UserId == t.MentorId);
        var project = await t.Db.Projects.SingleAsync(p => p.ProjectId == t.Seed.ProjectId);
        switch (scenario)
        {
            case "mentor-role": t.Db.UserRoles.RemoveRange(roles); break;
            case "ended": mentor.EndsAt = DateTimeOffset.UtcNow.AddHours(-1); break;
            case "future": mentor.StartsAt = DateTimeOffset.UtcNow.AddHours(1); break;
            case "other-cohort": t.Db.CohortMemberships.Remove(mentor); break;
            case "candidate-ended":
                (await t.Db.CohortMemberships.SingleAsync(m => m.CohortId == t.Seed.CohortId && m.UserId == t.Seed.CandidateId)).EndsAt = DateTimeOffset.UtcNow.AddHours(-1); break;
            case "project": project.IsArchived = true; project.ArchivedAt = DateTimeOffset.UtcNow; project.ArchivedBy = t.Seed.AdministratorId; project.ArchiveReason = "Test"; break;
            case "cycle":
                var cycle = await t.Db.LearningCycles.SingleAsync(c => c.CycleId == project.CycleId);
                cycle.IsArchived = true; cycle.ArchivedAt = DateTimeOffset.UtcNow; cycle.ArchivedBy = t.Seed.AdministratorId; cycle.ArchiveReason = "Test"; break;
            case "cohort":
                var cohort = await t.Db.Cohorts.SingleAsync(c => c.CohortId == t.Seed.CohortId);
                cohort.IsArchived = true; cohort.ArchivedAt = DateTimeOffset.UtcNow; cohort.ArchivedBy = t.Seed.AdministratorId; cohort.ArchiveReason = "Test"; break;
            case "team": project.Mode = ProjectMode.Team; break;
            case "self":
                actor = t.Seed.CandidateId;
                Assert.True((await t.Services.GetRequiredService<UserManager<IdentityUser>>().AddToRoleAsync(
                    (await t.Db.Users.SingleAsync(u => u.Id == actor)), "Mentor")).Succeeded);
                t.Db.CohortMemberships.Add(new() { CohortId = t.Seed.CohortId, UserId = actor,
                    MembershipKind = MembershipKind.Mentor, StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                    CreatedBy = t.Seed.AdministratorId, ChangeReason = "Dual-role self-review test" });
                break;
        }
        await t.Db.SaveChangesAsync();
        try
        {
            var result = await t.Reviews.DecideAsync(request, actor);
            Assert.Equal(scenario == "candidate-ended", result.Succeeded);
            if (scenario != "candidate-ended") Assert.False(await t.Db.SubmissionReviews.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
            Assert.False((await t.Reviews.DecideAsync(request, t.Seed.AdministratorId)).Succeeded);
        }
        finally
        {
            if (scenario == "mentor-role") { t.Db.UserRoles.AddRange(roles); await t.Db.SaveChangesAsync(); }
            if (scenario == "self")
                await t.Services.GetRequiredService<UserManager<IdentityUser>>().RemoveFromRoleAsync(await t.Db.Users.SingleAsync(u => u.Id == actor), "Mentor");
        }
    }

    [Theory]
    [InlineData("role")]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("cohort")]
    public async Task Reopening_rechecks_administrator_role_and_unarchived_ancestors(string scenario)
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        await t.ApproveAsync();
        var request = await t.ReopeningAsync();
        Assert.False((await t.Reopenings.ReopenAsync(request, t.MentorId)).Succeeded);
        Assert.False((await t.Reopenings.ReopenAsync(request, t.Seed.CandidateId)).Succeeded);
        var roles = await t.Db.UserRoles.Where(r => r.UserId == t.Seed.AdministratorId).ToListAsync();
        if (scenario == "role") t.Db.UserRoles.RemoveRange(roles);
        else if (scenario == "project")
        {
            var p = await t.Db.Projects.SingleAsync(p => p.ProjectId == t.Seed.ProjectId);
            p.IsArchived = true; p.ArchivedAt = DateTimeOffset.UtcNow; p.ArchivedBy = t.Seed.AdministratorId; p.ArchiveReason = "Test";
        }
        else if (scenario == "cycle")
        {
            var id = await t.Db.Projects.Where(p => p.ProjectId == t.Seed.ProjectId).Select(p => p.CycleId).SingleAsync();
            var c = await t.Db.LearningCycles.SingleAsync(c => c.CycleId == id);
            c.IsArchived = true; c.ArchivedAt = DateTimeOffset.UtcNow; c.ArchivedBy = t.Seed.AdministratorId; c.ArchiveReason = "Test";
        }
        else
        {
            var c = await t.Db.Cohorts.SingleAsync(c => c.CohortId == t.Seed.CohortId);
            c.IsArchived = true; c.ArchivedAt = DateTimeOffset.UtcNow; c.ArchivedBy = t.Seed.AdministratorId; c.ArchiveReason = "Test";
        }
        await t.Db.SaveChangesAsync();
        try
        {
            Assert.Null(await t.Reopenings.GetReopeningAsync(t.Seed.AssignmentId, t.Seed.AdministratorId));
            Assert.False((await t.Reopenings.ReopenAsync(request, t.Seed.AdministratorId)).Succeeded);
            Assert.False(await t.Db.AssignmentReopenings.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        }
        finally { if (scenario == "role") { t.Db.UserRoles.AddRange(roles); await t.Db.SaveChangesAsync(); } }
    }

    [Fact]
    public async Task Latest_attempt_and_stale_tokens_are_enforced_and_maximum_lengths_are_accepted()
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var stale = await t.DecisionAsync();
        await t.ReviseAsync(deadlineOnly: true);
        Assert.True((await t.Reviews.DecideAsync(stale, t.MentorId)).Conflict);
        Assert.True((await t.Submissions.SubmitAsync(await t.NewRequestAsync(), t.Seed.CandidateId)).Succeeded);
        var latest = await t.DecisionAsync(feedback: new string('f', ReviewLimits.Feedback));
        Assert.False((await t.Reviews.DecideAsync(latest with { SubmissionId = stale.SubmissionId }, t.MentorId)).Succeeded);
        Assert.True((await t.Reviews.DecideAsync(latest, t.MentorId)).Succeeded);
        var reopening = await t.ReopeningAsync(new string('r', ReviewLimits.ReopeningReason));
        Assert.False((await t.Reopenings.ReopenAsync(reopening with { Reason = " " }, t.Seed.AdministratorId)).Succeeded);
        Assert.False((await t.Reopenings.ReopenAsync(reopening with { Reason = new string('r', ReviewLimits.ReopeningReason + 1) }, t.Seed.AdministratorId)).Succeeded);
        await t.ReviseAsync(deadlineOnly: true);
        Assert.True((await t.Reopenings.ReopenAsync(reopening, t.Seed.AdministratorId)).Conflict);
        Assert.True((await t.Reopenings.ReopenAsync(await t.ReopeningAsync(new string('r', ReviewLimits.ReopeningReason)), t.Seed.AdministratorId)).Succeeded);
        Assert.True((await t.Submissions.SubmitAsync(await t.NewRequestAsync(), t.Seed.CandidateId)).Succeeded);
    }
}
