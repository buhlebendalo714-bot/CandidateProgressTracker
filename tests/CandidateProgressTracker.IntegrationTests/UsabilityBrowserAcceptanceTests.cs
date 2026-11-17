using System.Net;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class UsabilityBrowserAcceptanceTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [UsabilityBrowserFact]
    public async Task Core_submission_and_review_errors_are_keyboard_usable_at_SRS_widths_in_installed_browsers()
    {
        await using var individual = await ReviewTestContext.CreateAsync(fixture);
        await using var team = await TeamTestContext.CreateAsync(fixture);
        foreach (var item in new[] { (individual.Seed.AssignmentId, individual.Seed.CandidateId, false), (team.Seed.AssignmentId, team.Seed.CandidateId, true) })
        {
            using var candidate = await fixture.CreateClientAsync("Candidate", item.Item2);
            var path = $"/Candidate/{(item.Item3 ? "TeamSubmit" : "Submit")}/{item.AssignmentId}";
            using var opened = await candidate.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
            SubmissionPageTests.CopyResponseCookies(candidate, opened);
            var html = await opened.Content.ReadAsStringAsync();
            var names = new List<string> { "__RequestVerificationToken", "Input.RevisionId", "Input.RequestId" };
            if (item.Item3) names.Add("Input.RosterConfirmation");
            var fields = names.ToDictionary(n => n, n => SubmissionPageTests.Hidden(html, n));
            fields["Input.Explanation"] = "Retained explanation for usability verification";
            fields["Input.Evidence[0].Label"] = new string('x', 201);
            fields["Input.Evidence[0].Url"] = "https://example.com/evidence";
            using var rejected = await candidate.PostAsync(path, new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            var errorPage = await rejected.Content.ReadAsStringAsync();
            Assert.Contains("validation-summary-errors", errorPage);
            Assert.Contains("field-validation-error", errorPage);
            Assert.Equal(fields["Input.Evidence[0].Label"], SubmissionPageTests.Hidden(errorPage, "Input.Evidence[0].Label"));
            await VerifyInstalledBrowsersAsync(candidate, path, errorPage, item.Item3 ? "team" : "individual");
        }
        using var mentor = await fixture.CreateClientAsync("Mentor", individual.MentorId);
        var reviewPath = $"/Mentor/ReviewSubmission/{individual.Seed.AssignmentId}";
        var decision = await ReviewWorkflowPageTests.FormAsync(mentor, reviewPath, "SubmissionId");
        decision["Input.Decision"] = "2";
        decision["Input.Feedback"] = " ";
        using var invalidReview = await mentor.PostAsync(reviewPath, new FormUrlEncodedContent(decision));
        Assert.Equal(HttpStatusCode.OK, invalidReview.StatusCode);
        var reviewHtml = await invalidReview.Content.ReadAsStringAsync();
        Assert.Contains("requires feedback", reviewHtml);
        await VerifyInstalledBrowsersAsync(mentor, reviewPath, reviewHtml, "review");
        Assert.Equal(1, await individual.Db.Submissions.CountAsync(s => s.AssignmentId == individual.Seed.AssignmentId));
        Assert.False(await individual.Db.SubmissionReviews.AnyAsync(r => r.AssignmentId == individual.Seed.AssignmentId));
        Assert.False(await team.Db.Submissions.AnyAsync(s => s.AssignmentId == team.Seed.AssignmentId));
        Assert.False(await team.Db.AssignmentCreditedRosters.AnyAsync(r => r.AssignmentId == team.Seed.AssignmentId));
    }

    private static async Task VerifyInstalledBrowsersAsync(HttpClient client, string path, string html, string journey)
    {
        foreach (var browser in new[]
        {
            ("chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            ("edge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe")
        })
        {
            Assert.True(File.Exists(browser.Item2), "The selected installed browser is required; no software is installed by this test.");
            await CandidateDashboardTests.VerifyBrowserAsync(client, path, html, "usability-workspace.cjs", $"usability/{browser.Item1}-{journey}", browser.Item2);
        }
    }
}

internal sealed class UsabilityBrowserFactAttribute : FactAttribute
{
    public UsabilityBrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CPT_USABILITY_BROWSER") != "1")
            Skip = "Opt-in installed-browser usability acceptance using disposable data.";
    }
}
