using System.Net;
using System.Text.RegularExpressions;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class TeamSubmissionPageTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Candidate_post_requires_antiforgery_and_authoritative_confirmation_and_history_is_encoded()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        using var client = await fixture.CreateClientAsync("Candidate", t.Seed.CandidateId);
        var path = $"/Candidate/TeamSubmit/{t.Seed.AssignmentId}";
        using var form = await client.GetAsync(path);
        var html = await form.Content.ReadAsStringAsync();
        SubmissionPageTests.CopyResponseCookies(client, form);
        Assert.Contains("Confirm credited roster", html); Assert.Contains("Shared &lt;team&gt;", html);
        var input = Fields(html);
        input["Input.Explanation"] = "Team work <script>alert(1)</script>";
        input["Input.Evidence[0].Label"] = "Evidence <b>label</b>";
        input["Input.Evidence[0].Url"] = "https://example.com/team";
        using (var missing = await client.PostAsync(path, new FormUrlEncodedContent(input.Where(p => p.Key != "__RequestVerificationToken"))))
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var confirmation = input["Input.RosterConfirmation"]; input["Input.RosterConfirmation"] = "forged";
        using (var forged = await client.PostAsync(path, new FormUrlEncodedContent(input)))
        { Assert.Equal(HttpStatusCode.OK, forged.StatusCode); Assert.Contains("Roster confirmation", await forged.Content.ReadAsStringAsync()); }
        Assert.False(await t.Db.AssignmentCreditedRosters.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        input["Input.RosterConfirmation"] = confirmation;
        using (var accepted = await client.PostAsync(path, new FormUrlEncodedContent(input)))
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        var history = await client.GetStringAsync($"/Candidate/TeamSubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Contains("&lt;script&gt;", history); Assert.DoesNotContain("<script>alert", history);
        Assert.Contains("Evidence &lt;b&gt;label&lt;/b&gt;", history); Assert.Contains("Submitted by", history); Assert.Contains("Recorded team members", history);
        Assert.Contains("href=\"https://example.com/team\"", history); Assert.Contains("SAST", history);
        var attempt = await t.Db.Submissions.SingleAsync(s => s.AssignmentId == t.Seed.AssignmentId);
        var evidence = await t.Db.SubmissionEvidence.SingleAsync(e => e.SubmissionId == attempt.SubmissionId);
        evidence.Url = "javascript:alert(1)"; await t.Db.SaveChangesAsync();
        history = await client.GetStringAsync($"/Candidate/TeamSubmissionHistory/{t.Seed.AssignmentId}");
        Assert.DoesNotContain("href=\"javascript:", history);
    }

    [Fact]
    public async Task Former_member_routes_and_navigation_deny_data_while_authorized_viewers_keep_credit_and_feedback()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        using var former = await fixture.CreateClientAsync("Candidate", t.Seed.CandidateId);
        var submitPath = $"/Candidate/TeamSubmit/{t.Seed.AssignmentId}";
        using var openedForm = await former.GetAsync(submitPath);
        SubmissionPageTests.CopyResponseCookies(former, openedForm);
        var oldInput = Fields(await openedForm.Content.ReadAsStringAsync());
        oldInput["Input.Explanation"] = "Stale former-member submission";
        oldInput["Input.Evidence[0].Label"] = "Evidence";
        oldInput["Input.Evidence[0].Url"] = "https://example.com/former";
        await t.SubmitAsync();
        await t.EndTeamAsync(t.Seed.CandidateId); await t.SubmitAsync(t.OtherId);
        Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(ReviewDecision.ChangesRequested, "Team feedback <safe>"), t.MentorId)).Succeeded);
        Assert.DoesNotContain(t.Seed.AssignmentId.ToString(), await former.GetStringAsync("/Candidate/CreditedTeamHistory"));
        Assert.DoesNotContain(t.Seed.AssignmentId.ToString(), await former.GetStringAsync("/Candidate/Assignments"));
        foreach (var route in new[] { "TeamSubmit", "TeamSubmissionHistory", "Submit", "SubmissionHistory" })
        {
            using var denied = await former.GetAsync($"/Candidate/{route}/{t.Seed.AssignmentId}");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            var body = await denied.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Team feedback", body); Assert.DoesNotContain("https://example.com/evidence", body);
            Assert.DoesNotContain("Recorded team members", body);
        }
        using (var deniedPost = await former.PostAsync(submitPath, new FormUrlEncodedContent(oldInput)))
            Assert.Equal(HttpStatusCode.NotFound, deniedPost.StatusCode);
        Assert.Equal(2, await t.Db.Submissions.CountAsync(s => s.AssignmentId == t.Seed.AssignmentId));
        var joiner = await t.AddUserAsync();
        using var current = await fixture.CreateClientAsync("Candidate", joiner);
        var history = await current.GetStringAsync($"/Candidate/TeamSubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Contains("Team feedback &lt;safe&gt;", history);
        var frozenName = (await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, joiner))!.Roster!.Members.Single(m => m.UserId == t.Seed.CandidateId).UserName;
        Assert.Contains(frozenName, history);
        Assert.False((await t.Teams.GetFormAsync(t.Seed.AssignmentId, joiner))!.CanSubmit);
        var outsider = await t.AddUserAsync(team: false);
        using var other = await fixture.CreateClientAsync("Candidate", outsider);
        foreach (var route in new[] { "TeamSubmit", "TeamSubmissionHistory" })
        { using var response = await other.GetAsync($"/Candidate/{route}/{t.Seed.AssignmentId}"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
        using var deniedMentorRoute = await former.GetAsync($"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}");
        Assert.Equal(HttpStatusCode.Redirect, deniedMentorRoute.StatusCode);
        using var mentor = await fixture.CreateClientAsync("Mentor", t.MentorId);
        var review = await mentor.GetStringAsync($"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}"); Assert.Contains("Recorded team members", review);
        Assert.Contains(frozenName, review);
        using var administrator = await fixture.CreateClientAsync("Administrator", t.Seed.AdministratorId);
        Assert.Contains(frozenName, await administrator.GetStringAsync($"/Admin/Projects/ReopenAssignment/{t.Seed.AssignmentId}"));
        using var noToken = await mentor.PostAsync($"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
    }

    [Fact]
    public async Task Shared_history_pages_twenty_newest_attempts_and_preserves_original_revision_and_deadline()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        for (var i = 0; i < 21; i++) await t.SubmitAsync(i % 2 == 0 ? t.Seed.CandidateId : t.OtherId);
        await t.ReviseAsync(requirements: true);
        var first = (await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, t.Seed.CandidateId))!;
        var second = (await t.Teams.GetHistoryAsync(t.Seed.AssignmentId, t.OtherId, 2))!;
        Assert.Equal(20, first.Attempts.Count); Assert.True(first.HasNextPage); Assert.Single(second.Attempts); Assert.False(second.HasNextPage);
        Assert.Equal(Enumerable.Range(2, 20).Reverse(), first.Attempts.Select(a => a.AttemptNo));
        Assert.All(first.Attempts, a => { Assert.Equal(t.Seed.RevisionId, a.RevisionId); Assert.Equal("Complete the work.", a.Instructions); });
        using var client = await fixture.CreateClientAsync("Candidate", t.OtherId);
        var html = await client.GetStringAsync($"/Candidate/TeamSubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Contains("pageNumber=2", html);
    }
    private static Dictionary<string, string> Fields(string html) => new[] { "__RequestVerificationToken", "Input.RevisionId", "Input.RequestId", "Input.RosterConfirmation" }
        .ToDictionary(name => name, name => SubmissionPageTests.Hidden(html, name));

    [Fact]
    public async Task Team_review_and_reopening_http_preserve_feedback_stale_tokens_antiforgery_and_roster()
    {
        await using var t = await TeamTestContext.CreateAsync(fixture); await t.SubmitAsync();
        using var mentor = await fixture.CreateClientAsync("Mentor", t.MentorId);
        using var admin = await fixture.CreateClientAsync("Administrator", t.Seed.AdministratorId);
        using var candidate = await fixture.CreateClientAsync("Candidate", t.Seed.CandidateId);
        var reviewPath = $"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}";
        var reopenPath = $"/Admin/Projects/ReopenAssignment/{t.Seed.AssignmentId}";
        var fields = await ReviewWorkflowPageTests.FormAsync(mentor, reviewPath, "SubmissionId");
        fields["Input.Decision"] = "1"; fields["Input.Feedback"] = "Team approved <safe>";
        await t.ReviseAsync();
        using (var stale = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(fields))) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        fields = await ReviewWorkflowPageTests.FormAsync(mentor, reviewPath, "SubmissionId");
        fields["Input.Decision"] = "1"; fields["Input.Feedback"] = "Team approved <safe>";
        using (var accepted = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(fields))) Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.False((await t.Teams.GetFormAsync(t.Seed.AssignmentId, t.OtherId))!.CanSubmit);
        var history = await candidate.GetStringAsync($"/Candidate/TeamSubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Contains("Effective approval", history); Assert.Contains("Team approved &lt;safe&gt;", history);
        using (var noToken = await admin.PostAsync(reopenPath, new FormUrlEncodedContent(new Dictionary<string, string>()))) Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        var reopening = await ReviewWorkflowPageTests.FormAsync(admin, reopenPath, "ApprovalReviewId");
        reopening["Input.Reason"] = "Reconsider <safe>";
        using (var accepted = await admin.PostAsync(reopenPath, new FormUrlEncodedContent(reopening))) Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.True((await t.Teams.GetFormAsync(t.Seed.AssignmentId, t.OtherId))!.CanSubmit);
        history = await candidate.GetStringAsync($"/Candidate/TeamSubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Contains("Reopened approval", history); Assert.Contains("Reconsider &lt;safe&gt;", history);
        Assert.Equal(2, (await t.Teams.GetFormAsync(t.Seed.AssignmentId, t.OtherId))!.Roster!.Members.Count);
    }
}
