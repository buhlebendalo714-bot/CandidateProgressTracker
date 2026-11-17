using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CandidateProgressTracker.Application.Learning;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class WorkspacePresentationTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Authorised_page_groups_keep_forms_routes_and_responsive_workspace()
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        var cycle = await t.Db.Assignments.Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.CycleId).SingleAsync();
        var courses = t.Services.GetRequiredService<ICourseService>();
        var courseTitle = "Workspace certificate " + Guid.NewGuid().ToString("N");
        Assert.Null(await courses.CreateAsync(courseTitle, "Learning provider", "https://example.com/course", t.Seed.AdministratorId));
        var course = (await courses.GetSetupAsync(cycle, t.Seed.AdministratorId))!.Courses.Single(c => c.Title == courseTitle);
        Assert.Null(await courses.OfferAsync(course.CourseId, cycle, t.Seed.AdministratorId));
        var offering = (await courses.GetSetupAsync(cycle, t.Seed.AdministratorId))!.Offerings.Single(c => c.Title == courseTitle);
        Assert.Null(await courses.AssignAsync(offering.CourseId, t.Seed.CandidateId, DateTimeOffset.UtcNow.AddDays(7), "Complete the course", "Certificate demonstrates completion", "Actual certificate file required", true, t.Seed.AdministratorId));
        var groups = new[]
        {
            (Role: "Candidate", User: t.Seed.CandidateId, Paths: new[] { "/Candidate", "/Candidate/Courses", "/Candidate/Assignments", $"/Candidate/Submit/{t.Seed.AssignmentId}", $"/Candidate/SubmissionHistory/{t.Seed.AssignmentId}", $"/Candidate/Progress?cycleId={cycle}" }),
            (Role: "Mentor", User: t.MentorId, Paths: new[] { "/Mentor", "/Mentor/ReviewQueue", $"/Mentor/ReviewSubmission/{t.Seed.AssignmentId}", $"/Mentor/Progress?cycleId={cycle}", "/Mentor/Courses", "/Mentor/Assignments" }),
            (Role: "Administrator", User: t.Seed.AdministratorId, Paths: new[] { "/Admin", "/Admin/Users", "/Admin/Users/Create", "/Admin/Cohorts", $"/Admin/Courses/{cycle}", $"/Admin/Projects/{cycle}", "/Admin/Archives", "/Admin/Audit" })
        };
        foreach (var group in groups)
        {
            using var client = await fixture.CreateClientAsync(group.Role, group.User);
            foreach (var path in group.Paths)
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var html = await response.Content.ReadAsStringAsync();
                Assert.Contains("workspace-page", html);
                Assert.Contains("Skip to content", html);
                Assert.Contains("aria-label=\"Breadcrumb\"", html);
                if (path.Contains("/Submit/") || path.Contains("/ReviewSubmission/"))
                {
                    Assert.Contains("__RequestVerificationToken", html);
                    Assert.Contains("Input.RequestId", html);
                }
                if (Visual(path))
                    await CandidateDashboardTests.VerifyBrowserAsync(client, path, html, "workspace-pages.cjs", "workspace-visual/" + group.Role + "-" + path.Split('?')[0].Replace('/', '-'));
            }
        }
        await using var team = await TeamTestContext.CreateAsync(fixture);
        using var teamClient = await fixture.CreateClientAsync("Candidate", team.Seed.CandidateId);
        var teamPath = $"/Candidate/TeamSubmit/{team.Seed.AssignmentId}";
        var teamHtml = await teamClient.GetStringAsync(teamPath);
        Assert.Contains("Input.RosterConfirmation", teamHtml);
        Assert.Contains("assignment-workspace", teamHtml);
        if (Visual(teamPath))
            await CandidateDashboardTests.VerifyBrowserAsync(teamClient, teamPath, teamHtml, "workspace-pages.cjs", "workspace-visual/Candidate-team");
        using var anonymous = await fixture.CreateClientAsync();
        foreach (var path in new[] { "/Account/Login", "/Account/CompleteCredential" })
        {
            var html = await anonymous.GetStringAsync(path);
            Assert.Contains("__RequestVerificationToken", html);
            if (Visual(path))
                await CandidateDashboardTests.VerifyBrowserAsync(anonymous, path, html, "workspace-pages.cjs", "workspace-visual/" + path.Replace('/', '-'));
        }
    }

    private static bool Visual(string path) => Environment.GetEnvironmentVariable("CPT_WORKSPACE_VISUAL") == "1"
        && (Environment.GetEnvironmentVariable("CPT_WORKSPACE_RECHECK") != "1"
            || path.Contains("Submit/") || path.Contains("ReviewSubmission/") || path.Contains("Progress")
            || path is "/Candidate" or "/Candidate/Courses" or "/Mentor/Courses");
}
