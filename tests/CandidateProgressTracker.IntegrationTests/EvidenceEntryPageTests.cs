using System.Net;
using System.Text.RegularExpressions;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class EvidenceEntryPageTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Individual_rows_survive_validation_and_blank_rows_are_ignored()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        await VerifyAsync($"/Candidate/Submit/{seed.AssignmentId}", seed.CandidateId, seed.AssignmentId, db, false);
    }

    [Fact]
    public async Task Team_rows_survive_validation_and_blank_rows_are_ignored()
    {
        await using var team = await TeamTestContext.CreateAsync(fixture);
        await VerifyAsync($"/Candidate/TeamSubmit/{team.Seed.AssignmentId}", team.Seed.CandidateId, team.Seed.AssignmentId, team.Db, true);
    }

    private async Task VerifyAsync(string path, string actor, Guid assignmentId, TrackerDbContext db, bool team)
    {
        using var client = await fixture.CreateClientAsync("Candidate", actor);
        using var opened = await client.GetAsync(path);
        var html = await opened.Content.ReadAsStringAsync();
        SubmissionPageTests.CopyResponseCookies(client, opened);
        Assert.Single(Regex.Matches(html, "data-link-row>"));
        Assert.Contains("Add another link", html);
        Assert.Contains("HTTPS URL", html);
        var artifact = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "evidence-entry");
        Directory.CreateDirectory(artifact);
        await File.WriteAllTextAsync(Path.Combine(artifact, team ? "team.html" : "individual.html"),
            Regex.Match(html, "<section data-evidence-editor[\\s\\S]*?</section>").Value);
        var names = new List<string> { "__RequestVerificationToken", "Input.RevisionId", "Input.RequestId" };
        if (team) names.Add("Input.RosterConfirmation");
        var fields = names.ToDictionary(n => n, n => SubmissionPageTests.Hidden(html, n));
        fields["Input.Explanation"] = "Evidence editor verification";
        fields["Input.Evidence[0].Label"] = "Kept <label>";
        fields["Input.Evidence[0].Url"] = "https://example.com/kept";
        fields["Input.Evidence[1].Label"] = "Partial label";
        fields["Input.Evidence[1].Url"] = "";
        fields["Input.Evidence[2].Label"] = "";
        fields["Input.Evidence[2].Url"] = "";
        using var invalid = await client.PostAsync(path, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var rejected = await invalid.Content.ReadAsStringAsync();
        Assert.Equal(3, Regex.Matches(rejected, "data-link-row>").Count);
        Assert.Equal("Kept <label>", SubmissionPageTests.Hidden(rejected, "Input.Evidence[0].Label"));
        Assert.Equal("https://example.com/kept", SubmissionPageTests.Hidden(rejected, "Input.Evidence[0].Url"));
        Assert.Equal("Partial label", SubmissionPageTests.Hidden(rejected, "Input.Evidence[1].Label"));
        Assert.Contains("validation-summary-errors", rejected);
        Assert.False(await db.Submissions.AnyAsync(s => s.AssignmentId == assignmentId));
        if (team) Assert.False(await db.AssignmentCreditedRosters.AnyAsync(r => r.AssignmentId == assignmentId));

        // A label length error must stay attached to its posted field.
        fields["Input.Evidence[1].Label"] = new string('x', 201);
        using var tooLong = await client.PostAsync(path, new FormUrlEncodedContent(fields));
        var lengthHtml = await tooLong.Content.ReadAsStringAsync();
        Assert.Contains("field-validation-error", lengthHtml);
        Assert.Equal(new string('x', 201), SubmissionPageTests.Hidden(lengthHtml, "Input.Evidence[1].Label"));

        // Clear the optional rows, retaining the first link and the original request identity.
        fields["Input.Evidence[1].Label"] = " ";
        using var accepted = await client.PostAsync(path, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        var submission = await db.Submissions.SingleAsync(s => s.AssignmentId == assignmentId);
        var evidence = await db.SubmissionEvidence.Where(e => e.SubmissionId == submission.SubmissionId).ToListAsync();
        Assert.Single(evidence);
        Assert.Equal("Kept <label>", evidence[0].Label);
    }
}
