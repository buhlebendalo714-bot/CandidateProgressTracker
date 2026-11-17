using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class TeamSubmissionTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task First_attempt_freezes_sources_snapshots_and_actor_and_other_assignments_freeze_independently()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var form = (await t.Teams.GetFormAsync(t.Seed.AssignmentId, t.Seed.CandidateId))!;
        Assert.Null(form.Roster); Assert.Equal(2, form.Preview.Count);
        var receipt = await t.SubmitAsync();
        var roster = await t.Db.AssignmentCreditedRosters.AsNoTracking().SingleAsync(r => r.AssignmentId == t.Seed.AssignmentId);
        Assert.Equal(t.Seed.CandidateId, roster.FrozenBy); Assert.Equal(t.TeamId, roster.TeamId);
        Assert.Equal(t.Seed.CohortId, roster.CohortId); Assert.Equal(TimeSpan.Zero, roster.FrozenAt.Offset);
        var members = await t.Db.AssignmentCreditedRosterMembers.Where(m => m.AssignmentId == t.Seed.AssignmentId).ToListAsync();
        Assert.Equal(form.Preview.OrderBy(m => m.UserId), members.OrderBy(m => m.UserId).Select(m => new CreditedMember(m.UserId, m.UserNameSnapshot, m.TeamMembershipId, m.CohortMembershipId)));
        var attempt = await t.Db.Submissions.SingleAsync(s => s.SubmissionId == receipt.SubmissionId);
        Assert.Equal(t.Seed.AssignmentId, attempt.CreditedRosterAssignmentId); Assert.Equal(t.Seed.CandidateId, attempt.SubmittedBy);
        var joiner = await t.AddUserAsync();
        var assignment = await t.Db.Assignments.AsNoTracking().SingleAsync(a => a.AssignmentId == t.Seed.AssignmentId);
        var milestone = new Milestone { ProjectId = t.Seed.ProjectId, SequenceNo = 2, Title = "New work", Instructions = "New", AcceptanceExpectations = "New", EvidencePolicy = "New", CreatedBy = t.Seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
        var next = new Assignment { ProjectId = assignment.ProjectId, CycleId = assignment.CycleId, MilestoneId = milestone.MilestoneId, TeamId = t.TeamId, DueAt = assignment.DueAt, CreatedBy = t.Seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
        var revision = new AssignmentRevision { AssignmentId = next.AssignmentId, RevisionNo = 1, DueAt = next.DueAt, InstructionsSnapshot = "New", AcceptanceSnapshot = "New", EvidencePolicySnapshot = "New", ChangedBy = t.Seed.AdministratorId, ChangedAt = DateTimeOffset.UtcNow, Reason = "Initial" };
        t.Db.AddRange(milestone, next, revision); await t.Db.SaveChangesAsync();
        var nextForm = (await t.Teams.GetFormAsync(next.AssignmentId, joiner))!;
        Assert.Equal(3, nextForm.Preview.Count);
        var request = new TeamSubmissionRequest(new(next.AssignmentId, revision.RevisionId, Guid.NewGuid(), "New assignment", new[] { new SubmissionEvidenceInput("Evidence", "https://example.com/new") }), nextForm.Confirmation);
        Assert.True((await t.Teams.SubmitAsync(request, joiner)).Succeeded);
        Assert.Equal(2, await t.Db.AssignmentCreditedRosterMembers.CountAsync(m => m.AssignmentId == t.Seed.AssignmentId));
        Assert.Equal(3, await t.Db.AssignmentCreditedRosterMembers.CountAsync(m => m.AssignmentId == next.AssignmentId));
    }

    [Fact]
    public async Task Joiners_read_without_credit_leavers_lose_access_but_keep_credit_and_empty_roster_blocks()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var original = await t.RequestAsync();
        Assert.True((await t.Teams.SubmitAsync(original, t.Seed.CandidateId)).Succeeded);
        var joiner = await t.AddUserAsync();
        var form = (await t.Teams.GetFormAsync(t.Seed.AssignmentId, joiner))!;
        Assert.False(form.CanSubmit); Assert.DoesNotContain(form.Roster!.Members, m => m.UserId == joiner);
        Assert.False((await t.Teams.SubmitAsync(await t.RequestAsync(joiner), joiner)).Succeeded);
        await t.EndTeamAsync(t.Seed.CandidateId);
        Assert.False((await t.Teams.SubmitAsync(original, t.Seed.CandidateId)).Succeeded); // Even an accepted retry needs current POST authorization.
        await t.SubmitAsync(t.OtherId);
        var decision = await t.DecisionAsync(ReviewDecision.ChangesRequested, "Shared feedback <encoded>");
        Assert.True((await t.Reviews.DecideAsync(decision, t.MentorId)).Succeeded);
        Assert.Null(await t.Teams.GetFormAsync(t.Seed.AssignmentId, t.Seed.CandidateId));
        Assert.Null(await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, t.Seed.CandidateId));
        Assert.DoesNotContain(await t.Teams.GetCreditedHistoryAsync(t.Seed.CandidateId), x => x.AssignmentId == t.Seed.AssignmentId);
        var history = (await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, joiner))!;
        Assert.Equal(2, history.Attempts.Count); Assert.Contains("Shared feedback", history.Attempts[0].Reviews.Single().Feedback);
        Assert.Contains(history.Roster!.Members, m => m.UserId == t.Seed.CandidateId);
        Assert.Contains((await t.Teams.GetMentorHistoryAsync(t.Seed.AssignmentId, t.MentorId))!.Roster!.Members, m => m.UserId == t.Seed.CandidateId);
        Assert.Contains((await t.Reopenings.GetReopeningAsync(t.Seed.AssignmentId, t.Seed.AdministratorId))!.Roster!.Members, m => m.UserId == t.Seed.CandidateId);
        await t.EndTeamAsync(t.OtherId);
        form = (await t.Teams.GetFormAsync(t.Seed.AssignmentId, joiner))!;
        Assert.False(form.CanSubmit); Assert.Contains("No credited", form.Eligibility);
        Assert.NotNull(await t.Teams.GetMentorHistoryAsync(t.Seed.AssignmentId, t.MentorId));
        Assert.NotNull(await t.Reviews.GetReviewAsync(t.Seed.AssignmentId, t.MentorId));
        Assert.Equal(2, await t.Db.AssignmentCreditedRosterMembers.CountAsync(m => m.AssignmentId == t.Seed.AssignmentId));
    }

    [Theory]
    [InlineData("forged")]
    [InlineData("join")]
    [InlineData("leave")]
    [InlineData("role")]
    [InlineData("scheduled-start")]
    [InlineData("scheduled-end")]
    public async Task Changed_or_forged_preview_is_rejected_without_partial_roster(string change)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var request = await t.RequestAsync();
        if (change == "forged") request = request with { RosterConfirmation = "forged" };
        if (change == "join") await t.AddUserAsync();
        if (change == "leave") await t.EndTeamAsync(t.OtherId);
        if (change == "role")
        {
            var users = t.Services.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(t.OtherId))!, "Candidate")).Succeeded);
        }
        if (change.StartsWith("scheduled-"))
        {
            var m = await t.Db.TeamMemberships.SingleAsync(m => m.UserId == t.OtherId && m.ProjectId == t.Seed.ProjectId);
            // Move the scheduled boundary deterministically; no clock sleeps.
            if (change == "scheduled-start") m.StartsAt = DateTimeOffset.UtcNow.AddDays(1);
            else m.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await t.Db.SaveChangesAsync();
        }
        var version = await t.Db.Assignments.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.RowVersion).SingleAsync();
        var result = await t.Teams.SubmitAsync(request, t.Seed.CandidateId);
        Assert.False(result.Succeeded); Assert.Contains(result.Errors, e => e.Contains("Roster confirmation"));
        Assert.False(await t.Db.AssignmentCreditedRosters.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.False(await t.Db.AssignmentCreditedRosterMembers.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.False(await t.Db.Submissions.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.Equal(version, await t.Db.Assignments.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.RowVersion).SingleAsync());
        Assert.True((await t.Teams.SubmitAsync(await t.RequestAsync(), t.Seed.CandidateId)).Succeeded);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("cohort")]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("archive-cohort")]
    [InlineData("cross-team")]
    public async Task Current_authorization_is_required_even_for_frozen_credit_and_accepted_retries(string denial)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var request = await t.RequestAsync(); await t.Teams.SubmitAsync(request, t.Seed.CandidateId);
        var actor = t.Seed.CandidateId;
        switch (denial)
        {
            case "role":
                var users = t.Services.GetRequiredService<UserManager<IdentityUser>>();
                await users.RemoveFromRoleAsync((await users.FindByIdAsync(actor))!, "Candidate"); break;
            case "cohort":
                (await t.Db.CohortMemberships.SingleAsync(m => m.CohortId == t.Seed.CohortId && m.UserId == actor)).EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1); break;
            case "project":
                var p = await t.Db.Projects.SingleAsync(p => p.ProjectId == t.Seed.ProjectId);
                p.IsArchived = true; p.ArchivedAt = DateTimeOffset.UtcNow; p.ArchivedBy = t.Seed.AdministratorId; p.ArchiveReason = "Test"; break;
            case "cycle":
                var c = await t.Db.LearningCycles.SingleAsync(c => c.CycleId == t.Db.Projects.Where(p => p.ProjectId == t.Seed.ProjectId).Select(p => p.CycleId).Single());
                c.IsArchived = true; c.ArchivedAt = DateTimeOffset.UtcNow; c.ArchivedBy = t.Seed.AdministratorId; c.ArchiveReason = "Test"; break;
            case "archive-cohort":
                var cohort = await t.Db.Cohorts.SingleAsync(c => c.CohortId == t.Seed.CohortId);
                cohort.IsArchived = true; cohort.ArchivedAt = DateTimeOffset.UtcNow; cohort.ArchivedBy = t.Seed.AdministratorId; cohort.ArchiveReason = "Test"; break;
            case "cross-team": actor = await t.AddUserAsync(team: false); break;
        }
        await t.Db.SaveChangesAsync();
        Assert.Null(await t.Teams.GetFormAsync(t.Seed.AssignmentId, actor));
        Assert.Null(await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, actor));
        Assert.DoesNotContain(await t.Teams.GetCreditedHistoryAsync(actor), a => a.AssignmentId == t.Seed.AssignmentId);
        Assert.False((await t.Teams.SubmitAsync(request, actor)).Succeeded);
        Assert.Single(await t.Db.Submissions.Where(s => s.AssignmentId == t.Seed.AssignmentId).ToListAsync());
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("revision")]
    [InlineData("explanation")]
    [InlineData("evidence")]
    public async Task Changed_request_reuse_is_rejected_and_ids_are_scoped_to_actual_submitter(string field)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var request = await t.RequestAsync(); var first = await t.Teams.SubmitAsync(request, t.Seed.CandidateId);
        var content = request.Content;
        var changed = field switch {
            "assignment" => content with { AssignmentId = Guid.NewGuid() },
            "revision" => content with { AssignmentRevisionId = Guid.NewGuid() },
            "explanation" => content with { Explanation = "Different" },
            _ => content with { Evidence = new[] { new SubmissionEvidenceInput("Other", "https://example.com/other") } }
        };
        Assert.False((await t.Teams.SubmitAsync(request with { Content = changed }, t.Seed.CandidateId)).Succeeded);
        Assert.Equal(first.Receipt, (await t.Teams.SubmitAsync(request, t.Seed.CandidateId)).Receipt);
        var other = await t.Teams.SubmitAsync(request, t.OtherId);
        Assert.True(other.Succeeded); Assert.NotEqual(first.Receipt, other.Receipt);
        Assert.Equal(2, await t.Db.Submissions.CountAsync(s => s.AssignmentId == t.Seed.AssignmentId));
    }
}
