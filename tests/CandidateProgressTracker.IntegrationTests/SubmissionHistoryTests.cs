using System.Net;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class SubmissionHistoryTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task History_preserves_revision_orders_and_paginates_and_renders_safe_text()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var revision = await db.AssignmentRevisions.SingleAsync(r => r.RevisionId == seed.RevisionId);
        var due = revision.DueAt;
        db.AssignmentRevisions.Add(new AssignmentRevision
        {
            AssignmentId = seed.AssignmentId, RevisionNo = 2, DueAt = due.AddDays(5),
            InstructionsSnapshot = "LATEST MUST NOT APPEAR", AcceptanceSnapshot = "New acceptance",
            EvidencePolicySnapshot = "New policy", ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = seed.AdministratorId, Reason = "History preservation test"
        });
        for (var i = 1; i <= 21; i++)
        {
            var received = due.AddTicks(i - 2);
            var submission = new Submission
            {
                AssignmentId = seed.AssignmentId, AssignmentRevisionId = seed.RevisionId,
                AttemptNo = i, RequestId = Guid.NewGuid(), SubmittedBy = seed.CandidateId,
                Explanation = "<script>alert('explanation')</script>", ReceivedAt = received, RecordedAt = received
            };
            db.Submissions.Add(submission);
            db.SubmissionEvidence.AddRange(
                new SubmissionEvidence { SubmissionId = submission.SubmissionId, SequenceNo = 1,
                    Label = "<img src=x onerror=alert(1)>", Url = "https://example.com/history" },
                new SubmissionEvidence { SubmissionId = submission.SubmissionId, SequenceNo = 2,
                    Label = "Unsafe", Url = "javascript:alert(1)" });
        }
        await db.SaveChangesAsync();
        var query = new IndividualSubmissionQueryService(db);
        var first = (await query.GetHistoryAsync(seed.AssignmentId, seed.CandidateId))!;
        var second = (await query.GetHistoryAsync(seed.AssignmentId, seed.CandidateId, 2))!;
        Assert.Equal(Enumerable.Range(2, 20).Reverse(), first.Attempts.Select(a => a.AttemptNo));
        Assert.True(first.HasNextPage);
        Assert.False(second.HasNextPage);
        Assert.Equal(1, Assert.Single(second.Attempts).AttemptNo);
        Assert.All(first.Attempts, a =>
        {
            Assert.Equal(seed.RevisionId, a.RevisionId);
            Assert.Equal(1, a.RevisionNo);
            Assert.Equal(due, a.DueAt);
            Assert.Equal("Complete the work.", a.Instructions);
            Assert.Equal("Provide working evidence.", a.AcceptanceExpectations);
            Assert.Equal("Provide an HTTPS link.", a.EvidencePolicy);
            Assert.Equal(2, a.Evidence.Count);
        });
        Assert.False(first.Attempts.Last().IsLate); // Exactly at the deadline.
        Assert.True(first.Attempts[^2].IsLate); // One tick after the deadline.
        Assert.False(second.Attempts[0].IsLate); // One tick before the deadline.
        Assert.Empty((await query.GetHistoryAsync(seed.AssignmentId, seed.CandidateId, 3))!.Attempts);

        using var client = await fixture.CreateClientAsync("Candidate");
        var path = $"/Candidate/SubmissionHistory/{seed.AssignmentId}";
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;img", html);
        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("href=\"javascript:", html);
        Assert.Contains("href=\"https://example.com/history\"", html);
        Assert.DoesNotContain("LATEST MUST NOT APPEAR", html);
        Assert.Contains(seed.RevisionId.ToString(), html);
        Assert.Contains(first.Attempts[0].ReceiptId.ToString(), html);
        Assert.Contains("Submission timing", html);
        Assert.Contains(due.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-dd HH:mm:ss.fffffff"), html);
        Assert.True(html.IndexOf("Attempt 21", StringComparison.Ordinal) < html.IndexOf("Attempt 20", StringComparison.Ordinal));
        var pageTwo = await client.GetStringAsync(path + "?pageNumber=2");
        Assert.Contains("Attempt 1</h2>", pageTwo);
        Assert.DoesNotContain("Attempt 2</h2>", pageTwo);
        var cards = await client.GetStringAsync("/Candidate/Assignments");
        Assert.Contains("Submission history", cards);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("membership")]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("cohort")]
    [InlineData("role")]
    [InlineData("team")]
    public async Task Ineligible_history_is_hidden_in_service_and_http(string scenario)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        // Issue the cookie before revoking the database role: service checks must still deny access.
        using var client = await fixture.CreateClientAsync("Candidate");
        var project = await db.Projects.SingleAsync(p => p.ProjectId == seed.ProjectId);
        var removedRoles = await db.UserRoles.Where(r => r.UserId == seed.CandidateId).ToListAsync();
        switch (scenario)
        {
            case "owner": (await db.Assignments.SingleAsync(a => a.AssignmentId == seed.AssignmentId)).CandidateId = seed.AdministratorId; break;
            case "membership": (await db.CohortMemberships.SingleAsync(m => m.CohortId == seed.CohortId)).EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1); break;
            case "project": project.IsArchived = true; project.ArchivedAt = DateTimeOffset.UtcNow; project.ArchivedBy = seed.AdministratorId; project.ArchiveReason = "Test"; break;
            case "cycle": var cycle = await db.LearningCycles.SingleAsync(c => c.CycleId == project.CycleId); cycle.IsArchived = true; cycle.ArchivedAt = DateTimeOffset.UtcNow; cycle.ArchivedBy = seed.AdministratorId; cycle.ArchiveReason = "Test"; break;
            case "cohort": var cohort = await db.Cohorts.SingleAsync(c => c.CohortId == seed.CohortId); cohort.IsArchived = true; cohort.ArchivedAt = DateTimeOffset.UtcNow; cohort.ArchivedBy = seed.AdministratorId; cohort.ArchiveReason = "Test"; break;
            case "role": db.UserRoles.RemoveRange(removedRoles); break;
            case "team": project.Mode = ProjectMode.Team; break;
        }
        await db.SaveChangesAsync();
        try
        {
            Assert.Null(await new IndividualSubmissionQueryService(db).GetHistoryAsync(seed.AssignmentId, seed.CandidateId));
            using var response = await client.GetAsync($"/Candidate/SubmissionHistory/{seed.AssignmentId}");
            if (scenario == "role")
            {
                // Current-role validation invalidates the stale cookie before
                // the page runs; the independent service denial above remains.
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
            }
            else Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("Submission test project", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            if (scenario == "role") { db.UserRoles.AddRange(removedRoles); await db.SaveChangesAsync(); }
        }
    }

    [Theory]
    [InlineData(null, "/Account/Login")]
    [InlineData("Mentor", "/Account/AccessDenied")]
    [InlineData("Administrator", "/Account/AccessDenied")]
    public async Task History_requires_candidate_role(string? role, string redirect)
    {
        using var client = await fixture.CreateClientAsync(role);
        using var response = await client.GetAsync($"/Candidate/SubmissionHistory/{fixture.AssignmentId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(redirect, new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }
}
