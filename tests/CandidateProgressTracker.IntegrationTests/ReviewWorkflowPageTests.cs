using System.Net;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CandidateProgressTracker.IntegrationTests.SubmissionPageTests;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class ReviewWorkflowPageTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData("Candidate", "Mentor/ReviewSubmission")]
    [InlineData("Administrator", "Mentor/ReviewSubmission")]
    [InlineData("Mentor", "Admin/Projects/ReopenAssignment")]
    [InlineData("Candidate", "Admin/Projects/ReopenAssignment")]
    [InlineData(null, "Mentor/ReviewSubmission")]
    [InlineData(null, "Admin/Projects/ReopenAssignment")]
    public async Task Routes_enforce_roles(string? role, string page)
    {
        using var client = await fixture.CreateClientAsync(role);
        using var response = await client.GetAsync($"/{page}/{fixture.AssignmentId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(role is null ? "/Account/Login" : "/Account/AccessDenied",
            new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }

    [Fact]
    public async Task Decisions_reopenings_feedback_and_antiforgery_work_through_http()
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        using var mentor = await fixture.CreateClientAsync("Mentor");
        using var admin = await fixture.CreateClientAsync("Administrator");
        using var candidate = await fixture.CreateClientAsync("Candidate");
        var reviewPath = $"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}";
        var reopenPath = $"/Admin/Projects/ReopenAssignment/{t.Seed.AssignmentId}";
        using (var noToken = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(new Dictionary<string, string>())))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        var fields = await FormAsync(mentor, reviewPath, "SubmissionId");
        fields["Input.Decision"] = "1";
        fields["Input.Feedback"] = "<script>Approved feedback</script>";
        using (var response = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(fields)))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using (var response = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(fields)))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var history = await candidate.GetStringAsync($"/Candidate/SubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Contains("Effective approval", history);
        Assert.Contains("&lt;script&gt;Approved feedback&lt;/script&gt;", history);
        Assert.DoesNotContain("<script>Approved feedback", history);
        Assert.Contains("Submission timing", history);
        using (var noToken = await admin.PostAsync(reopenPath, new FormUrlEncodedContent(new Dictionary<string, string>())))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        var reopening = await FormAsync(admin, reopenPath, "ApprovalReviewId");
        reopening["Input.Reason"] = "<b>Reconsider this approval</b>";
        await t.ReviseAsync(deadlineOnly: true);
        using (var staleReopening = await admin.PostAsync(reopenPath, new FormUrlEncodedContent(reopening)))
            Assert.Equal(HttpStatusCode.Conflict, staleReopening.StatusCode);
        reopening = await FormAsync(admin, reopenPath, "ApprovalReviewId");
        reopening["Input.Reason"] = "<b>Reconsider this approval</b>";
        using (var response = await admin.PostAsync(reopenPath, new FormUrlEncodedContent(reopening)))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using (var response = await admin.PostAsync(reopenPath, new FormUrlEncodedContent(reopening)))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        history = await candidate.GetStringAsync($"/Candidate/SubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Contains("Reopened approval", history);
        Assert.Contains("&lt;b&gt;Reconsider this approval&lt;/b&gt;", history);
        var stale = await FormAsync(mentor, reviewPath, "SubmissionId");
        stale["Input.Decision"] = "2";
        stale["Input.Feedback"] = "Please include another evidence link.";
        await t.ReviseAsync(deadlineOnly: true);
        using (var response = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(stale)))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var fresh = await FormAsync(mentor, reviewPath, "SubmissionId");
        fresh["Input.Decision"] = "2";
        fresh["Input.Feedback"] = "   ";
        using (var response = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(fresh)))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("requires feedback", await response.Content.ReadAsStringAsync());
        }
        fresh["Input.Feedback"] = "Please include another evidence link.";
        using (var response = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(fresh)))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(2, await t.Db.SubmissionReviews.CountAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.Single(await t.Db.AssignmentReopenings.Where(r => r.AssignmentId == t.Seed.AssignmentId).ToListAsync());
        Assert.Contains("Please include another evidence link.", await candidate.GetStringAsync($"/Candidate/SubmissionHistory/{t.Seed.AssignmentId}"));
    }

    [Fact]
    public async Task Http_denies_outside_scope_self_review_and_other_candidates_feedback()
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        await t.ApproveAsync();
        // Another Candidate may be a current cohort member and still cannot read this history.
        var users = t.Services.GetRequiredService<UserManager<IdentityUser>>();
        var other = new IdentityUser { UserName = $"review.other.{Guid.NewGuid():N}" };
        Assert.True((await users.CreateAsync(other, "Integration-Only!482")).Succeeded);
        Assert.True((await users.AddToRoleAsync(other, "Candidate")).Succeeded);
        t.Db.CohortMemberships.Add(new() { CohortId = t.Seed.CohortId, UserId = other.Id, MembershipKind = MembershipKind.Candidate,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedBy = t.Seed.AdministratorId, ChangeReason = "Isolation" });
        await t.Db.SaveChangesAsync();
        using var otherClient = await fixture.CreateClientAsync("Candidate", other.Id);
        using var hidden = await otherClient.GetAsync($"/Candidate/SubmissionHistory/{t.Seed.AssignmentId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.DoesNotContain("Effective approval", await hidden.Content.ReadAsStringAsync());

        using var mentor = await fixture.CreateClientAsync("Mentor");
        var membership = await t.Db.CohortMemberships.SingleAsync(m => m.CohortId == t.Seed.CohortId && m.UserId == t.MentorId);
        membership.EndsAt = DateTimeOffset.UtcNow.AddHours(-1);
        await t.Db.SaveChangesAsync();
        using var denied = await mentor.GetAsync($"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var candidateUser = await users.FindByIdAsync(t.Seed.CandidateId);
        Assert.True((await users.AddToRoleAsync(candidateUser!, "Mentor")).Succeeded);
        t.Db.CohortMemberships.Add(new() { CohortId = t.Seed.CohortId, UserId = t.Seed.CandidateId, MembershipKind = MembershipKind.Mentor,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedBy = t.Seed.AdministratorId, ChangeReason = "Self review" });
        await t.Db.SaveChangesAsync();
        try
        {
            using var dualRole = await fixture.CreateClientAsync("Candidate");
            var page = await dualRole.GetStringAsync($"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}");
            Assert.Contains("You cannot review your own submission.", page);
            Assert.DoesNotContain("Record decision", page);
        }
        finally { await users.RemoveFromRoleAsync(candidateUser!, "Mentor"); }
    }

    internal static async Task<Dictionary<string, string>> FormAsync(HttpClient client, string path, string target)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CopyResponseCookies(client, response);
        var html = await response.Content.ReadAsStringAsync();
        return new()
        {
            ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken"),
            [$"Input.{target}"] = Hidden(html, $"Input.{target}"),
            ["Input.RowVersion"] = Hidden(html, "Input.RowVersion"),
            ["Input.RequestId"] = Hidden(html, "Input.RequestId")
        };
    }
}
