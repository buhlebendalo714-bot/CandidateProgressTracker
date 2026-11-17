using System.Net;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CandidateProgressTracker.IntegrationTests.SubmissionPageTests;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class SubmissionAuthorizationTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Another_candidate_in_same_cohort_cannot_open_submit_or_read_history()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var service = scope.ServiceProvider.GetRequiredService<IIndividualSubmissionService>();
        var accepted = await service.SubmitAsync(IndividualSubmissionTests.Request(seed), seed.CandidateId);
        Assert.True(accepted.Succeeded, string.Join("; ", accepted.Errors));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var other = new IdentityUser { UserName = $"other.candidate.{Guid.NewGuid():N}" };
        Assert.True((await users.CreateAsync(other, "Integration-Only!482")).Succeeded);
        Assert.True((await users.AddToRoleAsync(other, "Candidate")).Succeeded);
        db.CohortMemberships.Add(new CohortMembership
        {
            CohortId = seed.CohortId, UserId = other.Id, MembershipKind = MembershipKind.Candidate,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedBy = seed.AdministratorId,
            ChangeReason = "Same-cohort ownership isolation test"
        });
        var original = await db.Assignments.SingleAsync(a => a.AssignmentId == seed.AssignmentId);
        var own = new Assignment
        {
            CycleId = original.CycleId, ProjectId = original.ProjectId, MilestoneId = original.MilestoneId,
            CandidateId = other.Id, DueAt = original.DueAt,
            CreatedAt = DateTimeOffset.UtcNow, CreatedBy = seed.AdministratorId
        };
        var revision = await db.AssignmentRevisions.AsNoTracking().SingleAsync(r => r.RevisionId == seed.RevisionId);
        var ownRevision = new AssignmentRevision
        {
            AssignmentId = own.AssignmentId, RevisionNo = 1, DueAt = own.DueAt,
            InstructionsSnapshot = revision.InstructionsSnapshot, AcceptanceSnapshot = revision.AcceptanceSnapshot,
            EvidencePolicySnapshot = revision.EvidencePolicySnapshot, ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = seed.AdministratorId, Reason = "Own assignment for antiforgery token"
        };
        db.Assignments.Add(own);
        db.AssignmentRevisions.Add(ownRevision);
        await db.SaveChangesAsync();
        using var client = await fixture.CreateClientAsync("Candidate", other.Id);
        var fields = await OpenFormAsync(client, own.AssignmentId);
        fields["Input.RevisionId"] = seed.RevisionId.ToString();
        var beforeSubmissions = await db.Submissions.CountAsync();
        var beforeEvidence = await db.SubmissionEvidence.CountAsync();

        var query = scope.ServiceProvider.GetRequiredService<IIndividualSubmissionQueryService>();
        Assert.Null(await query.GetAsync(seed.AssignmentId, other.Id));
        Assert.Null(await query.GetHistoryAsync(seed.AssignmentId, other.Id));
        var rejected = await service.SubmitAsync(IndividualSubmissionTests.Request(seed), other.Id);
        Assert.False(rejected.Succeeded);
        Assert.Null(rejected.Receipt);
        foreach (var path in new[] { $"/Candidate/Submit/{seed.AssignmentId}", $"/Candidate/SubmissionHistory/{seed.AssignmentId}" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(accepted.Receipt!.SubmissionId.ToString(), await response.Content.ReadAsStringAsync());
        }
        using var content = new FormUrlEncodedContent(fields);
        using var post = await client.PostAsync($"/Candidate/Submit/{seed.AssignmentId}", content);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode); // Valid antiforgery token; ownership denies the POST.
        Assert.Equal(beforeSubmissions, await db.Submissions.CountAsync());
        Assert.Equal(beforeEvidence, await db.SubmissionEvidence.CountAsync());
    }

    [Theory]
    [InlineData("role")]
    [InlineData("membership")]
    public async Task Revocation_after_opening_form_blocks_new_submissions_and_accepted_retries(string revoked)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var service = scope.ServiceProvider.GetRequiredService<IIndividualSubmissionService>();
        var acceptedRequest = IndividualSubmissionTests.Request(seed);
        Assert.True((await service.SubmitAsync(acceptedRequest, seed.CandidateId)).Succeeded);
        using var client = await fixture.CreateClientAsync("Candidate");
        var fields = await OpenFormAsync(client, seed.AssignmentId);
        var roles = await db.UserRoles.Where(r => r.UserId == seed.CandidateId).ToListAsync();
        if (revoked == "role") db.UserRoles.RemoveRange(roles);
        else
        {
            var membership = await db.CohortMemberships.SingleAsync(m => m.CohortId == seed.CohortId && m.UserId == seed.CandidateId);
            membership.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        }
        await db.SaveChangesAsync();
        try
        {
            var submissionsBefore = await db.Submissions.CountAsync();
            var evidenceBefore = await db.SubmissionEvidence.CountAsync();
            using var content = new FormUrlEncodedContent(fields);
            using var post = await client.PostAsync($"/Candidate/Submit/{seed.AssignmentId}", content);
            if (revoked == "role")
            {
                // Role-stale cookies are invalidated before the POST handler.
                Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
                Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, post.Headers.Location!).AbsolutePath);
            }
            else Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
            foreach (var request in new[] { acceptedRequest, acceptedRequest with { RequestId = Guid.NewGuid() } })
            {
                var result = await service.SubmitAsync(request, seed.CandidateId);
                Assert.False(result.Succeeded);
                Assert.Null(result.Receipt);
                Assert.Contains("This assignment is not available for submission.", result.Errors);
            }
            Assert.Equal(submissionsBefore, await db.Submissions.CountAsync());
            Assert.Equal(evidenceBefore, await db.SubmissionEvidence.CountAsync());
        }
        finally
        {
            if (revoked == "role") { db.UserRoles.AddRange(roles); await db.SaveChangesAsync(); }
        }
    }

    private static async Task<Dictionary<string, string>> OpenFormAsync(HttpClient client, Guid assignmentId)
    {
        using var response = await client.GetAsync($"/Candidate/Submit/{assignmentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CopyResponseCookies(client, response);
        var html = await response.Content.ReadAsStringAsync();
        return new()
        {
            ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken"),
            ["Input.RequestId"] = Hidden(html, "Input.RequestId"),
            ["Input.RevisionId"] = Hidden(html, "Input.RevisionId"),
            ["Input.Explanation"] = "Valid evidence after opening the form.",
            ["Input.Evidence[0].Label"] = "Repository",
            ["Input.Evidence[0].Url"] = "https://example.com/authorization-test"
        };
    }
}
