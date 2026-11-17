using System.Net;
using System.Text.RegularExpressions;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class StaffDashboardTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Mentor_dashboard_prioritises_eligible_reviews_separates_changes_and_keeps_current_scope()
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var original = await t.Db.Assignments.SingleAsync(a => a.AssignmentId == t.Seed.AssignmentId);
        var cohort = await t.Db.Cohorts.SingleAsync(c => c.CohortId == t.Seed.CohortId);
        cohort.Name = "Digital skills <cohort>";
        var milestone = new Milestone { ProjectId = t.Seed.ProjectId, SequenceNo = 2, Title = "Improve your evidence <safely>", Instructions = "Work", AcceptanceExpectations = "Evidence", EvidencePolicy = "Link", CreatedBy = t.Seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
        var assignment = new Assignment { CycleId = original.CycleId, ProjectId = original.ProjectId, MilestoneId = milestone.MilestoneId, CandidateId = t.Seed.CandidateId, DueAt = original.DueAt, CreatedBy = t.Seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
        var revision = new AssignmentRevision { AssignmentId = assignment.AssignmentId, RevisionNo = 1, DueAt = assignment.DueAt, InstructionsSnapshot = "Work", AcceptanceSnapshot = "Evidence", EvidencePolicySnapshot = "Link", ChangedBy = t.Seed.AdministratorId, ChangedAt = DateTimeOffset.UtcNow, Reason = "Staff dashboard test" };
        t.Db.AddRange(milestone, assignment, revision);
        await t.Db.SaveChangesAsync();
        var submitted = await t.Submissions.SubmitAsync(new(assignment.AssignmentId, revision.RevisionId, Guid.NewGuid(), "Evidence", [new("Work", "https://example.com/evidence")]), t.Seed.CandidateId);
        Assert.True(submitted.Succeeded);
        var review = (await t.Reviews.GetReviewAsync(assignment.AssignmentId, t.MentorId))!;
        Assert.True((await t.Reviews.DecideAsync(new(assignment.AssignmentId, submitted.Receipt!.SubmissionId, review.RowVersion, Guid.NewGuid(), ReviewDecision.ChangesRequested, "Please clarify"), t.MentorId)).Succeeded);
        using var mentor = await fixture.CreateClientAsync("Mentor", t.MentorId);
        var path = $"/Mentor?cycleId={original.CycleId}";
        var html = await mentor.GetStringAsync(path);
        Assert.Contains("Awaiting mentor review", html);
        Assert.Contains("Awaiting candidate changes", html);
        Assert.Contains("Cohort progress", html);
        Assert.Contains("Digital skills &lt;cohort&gt;", html);
        Assert.Contains("Improve your evidence &lt;safely&gt;", html);
        var pending = Regex.Matches(html, "class=\"mentor-review\" data-assignment-id=\"([^\"]+)\"");
        Assert.Single(pending);
        Assert.Equal(t.Seed.AssignmentId.ToString(), pending[0].Groups[1].Value);
        Assert.Contains($"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}", html);
        Assert.DoesNotContain($"/Mentor/ReviewSubmission/{assignment.AssignmentId}", html);
        Assert.DoesNotContain("/Admin/Users", html);
        if (Environment.GetEnvironmentVariable("CPT_STAFF_VISUAL") == "1")
            await CandidateDashboardTests.VerifyBrowserAsync(mentor, path, html, "staff-dashboard.cjs", "staff-mentor");

        var membership = await t.Db.CohortMemberships.SingleAsync(m => m.CohortId == t.Seed.CohortId && m.UserId == t.MentorId);
        membership.EndsAt = DateTimeOffset.UtcNow;
        await t.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await mentor.GetAsync(path)).StatusCode);
        using var candidate = await fixture.CreateClientAsync("Candidate", t.Seed.CandidateId);
        Assert.Equal(HttpStatusCode.Redirect, (await candidate.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Administrator_dashboard_groups_real_management_routes_and_authorised_activity()
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var cycle = await t.Db.Assignments.Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.CycleId).SingleAsync();
        using var admin = await fixture.CreateClientAsync("Administrator", t.Seed.AdministratorId);
        var path = $"/Admin?cohortId={t.Seed.CohortId}";
        using var response = await admin.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var html = await response.Content.ReadAsStringAsync();
        foreach (var label in new[] { "People", "Learning", "Governance", "Recent recorded activity", "Archived history", "Search audit history" }) Assert.Contains(label, html);
        Assert.Contains($"/Admin/Courses/{cycle}", html);
        Assert.Contains($"/Admin/Projects/{cycle}", html);
        Assert.Contains("/Admin/Users/Create", html);
        Assert.Contains("/Admin/Cohorts", html);
        Assert.DoesNotContain("/Mentor/ReviewSubmission", html);
        if (Environment.GetEnvironmentVariable("CPT_STAFF_VISUAL") == "1")
            await CandidateDashboardTests.VerifyBrowserAsync(admin, path, html, "staff-dashboard.cjs", "staff-admin");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/Admin?cohortId={Guid.NewGuid()}")).StatusCode);
        using var mentor = await fixture.CreateClientAsync("Mentor", t.MentorId);
        Assert.Equal(HttpStatusCode.Redirect, (await mentor.GetAsync(path)).StatusCode);
    }
}
