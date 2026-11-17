using System.Net;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class MentorSubmissionHistoryTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData("current", true)]
    [InlineData("candidate-ended", true)]
    [InlineData("another-cohort", false)]
    [InlineData("mentor-ended", false)]
    [InlineData("mentor-future", false)]
    [InlineData("removed-role", false)]
    [InlineData("project", false)]
    [InlineData("cycle", false)]
    [InlineData("cohort", false)]
    [InlineData("team", false)]
    public async Task Mentor_scope_is_enforced_in_service_and_http(string scenario, bool allowed)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var mentorId = await (from userRole in db.UserRoles
                              join role in db.Roles on userRole.RoleId equals role.Id
                              where role.NormalizedName == "MENTOR"
                              select userRole.UserId).SingleAsync();
        var mentorMembership = new CohortMembership
        {
            CohortId = seed.CohortId, UserId = mentorId, MembershipKind = MembershipKind.Mentor,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-2), CreatedBy = seed.AdministratorId,
            ChangeReason = "Mentor history test"
        };
        if (scenario != "another-cohort") db.CohortMemberships.Add(mentorMembership);
        var original = await db.AssignmentRevisions.SingleAsync(r => r.RevisionId == seed.RevisionId);
        var candidateName = await db.Users.Where(u => u.Id == seed.CandidateId).Select(u => u.UserName).SingleAsync();
        db.AssignmentRevisions.Add(new AssignmentRevision
        {
            AssignmentId = seed.AssignmentId, RevisionNo = 2, DueAt = original.DueAt.AddDays(10),
            InstructionsSnapshot = "LATEST REQUIREMENTS NOT SUBMITTED", AcceptanceSnapshot = "Latest acceptance",
            EvidencePolicySnapshot = "Latest policy", ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = seed.AdministratorId, Reason = "New revision after submission"
        });
        for (var attempt = 1; attempt <= 21; attempt++)
        {
            var received = original.DueAt.AddTicks(attempt - 2);
            var submission = new Submission
            {
                AssignmentId = seed.AssignmentId, AssignmentRevisionId = seed.RevisionId,
                AttemptNo = attempt, RequestId = Guid.NewGuid(), SubmittedBy = seed.CandidateId,
                ReceivedAt = received, RecordedAt = received, Explanation = "<script>private work</script>"
            };
            db.Submissions.Add(submission);
            db.SubmissionEvidence.AddRange(
                new SubmissionEvidence { SubmissionId = submission.SubmissionId, SequenceNo = 1,
                    Label = "<b>Evidence label</b>", Url = "https://example.com/mentor-history" },
                new SubmissionEvidence { SubmissionId = submission.SubmissionId, SequenceNo = 2,
                    Label = "Unsafe evidence", Url = "javascript:alert(1)" });
        }
        // Issue authentication before removing the database role to test stale cookie protection.
        using var client = await fixture.CreateClientAsync("Mentor");
        var removedRoles = await db.UserRoles.Where(r => r.UserId == mentorId).ToListAsync();
        var project = await db.Projects.SingleAsync(p => p.ProjectId == seed.ProjectId);
        switch (scenario)
        {
            case "candidate-ended":
                (await db.CohortMemberships.SingleAsync(m => m.CohortId == seed.CohortId && m.UserId == seed.CandidateId))
                    .EndsAt = DateTimeOffset.UtcNow.AddHours(-1);
                break;
            case "mentor-ended": mentorMembership.EndsAt = DateTimeOffset.UtcNow.AddHours(-1); break;
            case "mentor-future": mentorMembership.StartsAt = DateTimeOffset.UtcNow.AddDays(1); break;
            case "removed-role": db.UserRoles.RemoveRange(removedRoles); break;
            case "project":
                project.IsArchived = true; project.ArchivedAt = DateTimeOffset.UtcNow;
                project.ArchivedBy = seed.AdministratorId; project.ArchiveReason = "Test";
                break;
            case "cycle":
                var cycle = await db.LearningCycles.SingleAsync(c => c.CycleId == project.CycleId);
                cycle.IsArchived = true; cycle.ArchivedAt = DateTimeOffset.UtcNow;
                cycle.ArchivedBy = seed.AdministratorId; cycle.ArchiveReason = "Test";
                break;
            case "cohort":
                var cohort = await db.Cohorts.SingleAsync(c => c.CohortId == seed.CohortId);
                cohort.IsArchived = true; cohort.ArchivedAt = DateTimeOffset.UtcNow;
                cohort.ArchivedBy = seed.AdministratorId; cohort.ArchiveReason = "Test";
                break;
            case "team": project.Mode = ProjectMode.Team; break;
        }
        await db.SaveChangesAsync();
        try
        {
            var query = new IndividualSubmissionQueryService(db);
            var history = await query.GetMentorHistoryAsync(seed.AssignmentId, mentorId);
            var path = $"/Mentor/SubmissionHistory/{seed.AssignmentId}";
            using var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            if (!allowed)
            {
                Assert.Null(history);
                if (scenario == "removed-role")
                {
                    // Lifecycle invalidates role-stale sessions before the page.
                    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                    Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
                }
                else Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.DoesNotContain(candidateName!, html);
                Assert.DoesNotContain("private work", html);
                Assert.DoesNotContain(seed.RevisionId.ToString(), html);
                return;
            }

            Assert.NotNull(history);
            Assert.Equal(candidateName, history.CandidateName);
            Assert.True(history.HasNextPage);
            Assert.Equal(Enumerable.Range(2, 20).Reverse(), history.Attempts.Select(a => a.AttemptNo));
            Assert.All(history.Attempts, a =>
            {
                Assert.Equal(seed.RevisionId, a.RevisionId);
                Assert.Equal(original.DueAt, a.DueAt);
                Assert.Equal(original.InstructionsSnapshot, a.Instructions);
                Assert.Equal(original.AcceptanceSnapshot, a.AcceptanceExpectations);
                Assert.Equal(original.EvidencePolicySnapshot, a.EvidencePolicy);
            });
            Assert.False(history.Attempts[^1].IsLate);
            Assert.True(history.Attempts[^2].IsLate);
            var second = (await query.GetMentorHistoryAsync(seed.AssignmentId, mentorId, 2))!;
            Assert.Equal(1, Assert.Single(second.Attempts).AttemptNo);
            Assert.False(second.HasNextPage);
            Assert.False(second.Attempts[0].IsLate);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(candidateName!, html);
            Assert.Contains(history.Attempts[0].ReceiptId.ToString(), html);
            Assert.Contains(seed.RevisionId.ToString(), html);
            Assert.Contains("&lt;script&gt;private work&lt;/script&gt;", html);
            Assert.Contains("&lt;b&gt;Evidence label&lt;/b&gt;", html);
            Assert.Contains("href=\"https://example.com/mentor-history\"", html);
            Assert.DoesNotContain("href=\"javascript:", html);
            Assert.DoesNotContain("LATEST REQUIREMENTS NOT SUBMITTED", html);
            Assert.Contains(original.DueAt.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-dd HH:mm:ss.fffffff"), html);
            Assert.Contains(history.Attempts[0].ReceivedAt.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-dd HH:mm:ss.fffffff"), html);
            Assert.True(html.IndexOf("Attempt 21", StringComparison.Ordinal) < html.IndexOf("Attempt 20", StringComparison.Ordinal));
            var pageTwo = await client.GetStringAsync(path + "?pageNumber=2");
            Assert.Contains("Attempt 1</h2>", pageTwo);
            Assert.DoesNotContain("Attempt 2</h2>", pageTwo);
            var cards = await client.GetStringAsync("/Mentor/Assignments");
            Assert.Contains($"/Mentor/SubmissionHistory/{seed.AssignmentId}", cards);
            if (scenario == "candidate-ended")
                Assert.Null(await query.GetHistoryAsync(seed.AssignmentId, seed.CandidateId));
        }
        finally
        {
            if (scenario == "removed-role") { db.UserRoles.AddRange(removedRoles); await db.SaveChangesAsync(); }
        }
    }

    [Theory]
    [InlineData(null, "/Account/Login")]
    [InlineData("Candidate", "/Account/AccessDenied")]
    [InlineData("Administrator", "/Account/AccessDenied")]
    public async Task Mentor_history_route_denies_other_roles(string? role, string expectedPath)
    {
        using var client = await fixture.CreateClientAsync(role);
        using var response = await client.GetAsync($"/Mentor/SubmissionHistory/{fixture.AssignmentId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedPath, new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var candidateId = (await db.Assignments.SingleAsync(a => a.AssignmentId == fixture.AssignmentId)).CandidateId!;
        Assert.Null(await new IndividualSubmissionQueryService(db).GetMentorHistoryAsync(fixture.AssignmentId, candidateId));
    }
}
