using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class IndividualSubmissionTests
    : IClassFixture<WebAccessFixture>
{
    private readonly WebAccessFixture _fixture;

    public IndividualSubmissionTests(WebAccessFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Late_submission_saves_evidence_and_retry_preserves_original_revision_and_receipt()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await SeedAsync(db);
        var assignment = await db.Assignments.SingleAsync(item => item.AssignmentId == seed.AssignmentId);
        var originalRevision = await db.AssignmentRevisions.SingleAsync(item => item.RevisionId == seed.RevisionId);
        assignment.DueAt = originalRevision.DueAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        var service = new IndividualSubmissionService(db);
        var request = Request(seed);

        var first = await service.SubmitAsync(request, seed.CandidateId);
        db.AssignmentRevisions.Add(NewRevision(seed, originalRevision, 2));
        await db.SaveChangesAsync();
        var retry = await service.SubmitAsync(request, seed.CandidateId);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));
        Assert.True(retry.Succeeded, string.Join("; ", retry.Errors));
        Assert.NotNull(first.Receipt);
        Assert.Equal(first.Receipt, retry.Receipt);
        Assert.Equal(1, first.Receipt.AttemptNo);

        var saved = await db.Submissions.AsNoTracking()
            .SingleAsync(item => item.AssignmentId == seed.AssignmentId);

        Assert.Equal(seed.RevisionId, saved.AssignmentRevisionId);
        Assert.Equal(seed.CandidateId, saved.SubmittedBy);
        Assert.Equal(TimeSpan.Zero, saved.ReceivedAt.Offset);
        Assert.True(saved.RecordedAt >= saved.ReceivedAt);
        Assert.True(saved.ReceivedAt > originalRevision.DueAt);

        var evidence = await db.SubmissionEvidence.AsNoTracking()
            .SingleAsync(item => item.SubmissionId == saved.SubmissionId);

        Assert.Equal("Repository", evidence.Label);
        Assert.Equal("https://example.com/evidence", evidence.Url);
        Assert.Equal(1, evidence.SequenceNo);
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("revision")]
    [InlineData("explanation")]
    [InlineData("evidence-label")]
    [InlineData("evidence-url")]
    public async Task Reusing_request_id_with_changed_content_is_rejected(string field)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await SeedAsync(db);
        var service = new IndividualSubmissionService(db);
        var request = Request(seed);

        var first = await service.SubmitAsync(request, seed.CandidateId);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        var otherAssignment = await SeedAsync(db);
        var originalRevision = await db.AssignmentRevisions.SingleAsync(item => item.RevisionId == seed.RevisionId);
        var newRevision = NewRevision(seed, originalRevision, 2);
        db.AssignmentRevisions.Add(newRevision);
        await db.SaveChangesAsync();
        var changedRequest = field switch
        {
            "assignment" => request with { AssignmentId = otherAssignment.AssignmentId,
                AssignmentRevisionId = otherAssignment.RevisionId },
            "revision" => request with { AssignmentRevisionId = newRevision.RevisionId },
            "explanation" => request with { Explanation = "Different content." },
            "evidence-label" => request with { Evidence = new[] { request.Evidence[0] with { Label = "Changed label" } } },
            "evidence-url" => request with { Evidence = new[] { request.Evidence[0] with { Url = "https://example.com/changed" } } },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        var evidenceBefore = await db.SubmissionEvidence.CountAsync();
        var changed = await service.SubmitAsync(changedRequest, seed.CandidateId);

        Assert.False(changed.Succeeded);
        Assert.Contains("This request ID has already been used for different content.", changed.Errors);
        Assert.Equal(1, await db.Submissions.CountAsync(
            item => item.AssignmentId == seed.AssignmentId));
        Assert.False(await db.Submissions.AnyAsync(item => item.AssignmentId == otherAssignment.AssignmentId));
        Assert.Equal(evidenceBefore, await db.SubmissionEvidence.CountAsync());
        Assert.Equal(first.Receipt, (await service.SubmitAsync(request, seed.CandidateId)).Receipt);
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("ended-membership")]
    [InlineData("archived-project")]
    [InlineData("archived-cycle")]
    [InlineData("archived-cohort")]
    [InlineData("team-project")]
    [InlineData("team-owner")]
    [InlineData("wrong-revision")]
    [InlineData("unsafe-url")]
    [InlineData("blank-explanation")]
    [InlineData("no-evidence")]
    [InlineData("duplicate-evidence")]
    public async Task Invalid_submission_writes_nothing(string scenario)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await SeedAsync(db);
        var service = new IndividualSubmissionService(db);
        var request = Request(seed);
        var actor = seed.CandidateId;
        var evidenceBefore = await db.SubmissionEvidence.CountAsync();

        switch (scenario)
        {
            case "other-user":
                actor = seed.AdministratorId;
                break;

            case "ended-membership":
                var membership = await db.CohortMemberships.SingleAsync(
                    item => item.CohortId == seed.CohortId &&
                            item.UserId == seed.CandidateId);

                membership.EndsAt = DateTimeOffset.UtcNow.AddHours(-1);
                await db.SaveChangesAsync();
                break;

            case "archived-project":
                var project = await db.Projects.SingleAsync(
                    item => item.ProjectId == seed.ProjectId);

                project.IsArchived = true;
                project.ArchivedAt = DateTimeOffset.UtcNow;
                project.ArchivedBy = seed.AdministratorId;
                project.ArchiveReason = "Submission access test";
                await db.SaveChangesAsync();
                break;

            case "wrong-revision":
                request = request with
                {
                    AssignmentRevisionId = Guid.NewGuid()
                };
                break;

            case "archived-cycle":
                var cycleId = await db.Projects.Where(project => project.ProjectId == seed.ProjectId)
                    .Select(project => project.CycleId).SingleAsync();
                var cycle = await db.LearningCycles.SingleAsync(item => item.CycleId == cycleId);
                cycle.IsArchived = true;
                cycle.ArchivedAt = DateTimeOffset.UtcNow;
                cycle.ArchivedBy = seed.AdministratorId;
                cycle.ArchiveReason = "Submission access test";
                await db.SaveChangesAsync();
                break;

            case "archived-cohort":
                var cohort = await db.Cohorts.SingleAsync(item => item.CohortId == seed.CohortId);
                cohort.IsArchived = true;
                cohort.ArchivedAt = DateTimeOffset.UtcNow;
                cohort.ArchivedBy = seed.AdministratorId;
                cohort.ArchiveReason = "Submission access test";
                await db.SaveChangesAsync();
                break;

            case "team-project":
            case "team-owner":
                var teamProject = await db.Projects.SingleAsync(item => item.ProjectId == seed.ProjectId);
                teamProject.Mode = ProjectMode.Team;
                if (scenario == "team-owner")
                {
                    var team = new ProjectTeam { ProjectId = seed.ProjectId, Name = "Blocked team",
                        CreatedAt = DateTimeOffset.UtcNow, CreatedBy = seed.AdministratorId };
                    db.ProjectTeams.Add(team);
                    var assignment = await db.Assignments.SingleAsync(item => item.AssignmentId == seed.AssignmentId);
                    assignment.CandidateId = null;
                    assignment.TeamId = team.TeamId;
                }
                await db.SaveChangesAsync();
                break;

            case "unsafe-url":
                request = request with
                {
                    Evidence = new[]
                    {
                        new SubmissionEvidenceInput(
                            "Unsafe",
                            "javascript:alert(1)")
                    }
                };
                break;

            case "blank-explanation":
                request = request with { Explanation = "   " };
                break;

            case "no-evidence":
                request = request with
                {
                    Evidence = Array.Empty<SubmissionEvidenceInput>()
                };
                break;

            case "duplicate-evidence":
                request = request with
                {
                    Evidence = new[]
                    {
                        new SubmissionEvidenceInput(
                            "First", "https://example.com/evidence"),
                        new SubmissionEvidenceInput(
                            "Second", "https://example.com/evidence")
                    }
                };
                break;
        }

        var result = await service.SubmitAsync(request, actor);

        Assert.False(result.Succeeded);
        Assert.Null(result.Receipt);
        Assert.NotEmpty(result.Errors);

        Assert.False(await db.Submissions.AnyAsync(
            item => item.AssignmentId == seed.AssignmentId));
        Assert.Equal(evidenceBefore, await db.SubmissionEvidence.CountAsync());
    }

    private static AssignmentRevision NewRevision(Seed seed, AssignmentRevision original, int revisionNo) => new()
    {
        AssignmentId = seed.AssignmentId, RevisionNo = revisionNo, DueAt = original.DueAt,
        InstructionsSnapshot = "Changed requirements", AcceptanceSnapshot = original.AcceptanceSnapshot,
        EvidencePolicySnapshot = original.EvidencePolicySnapshot, ChangedAt = DateTimeOffset.UtcNow,
        ChangedBy = seed.AdministratorId, Reason = "Submission retry policy test"
    };

    internal static IndividualSubmissionRequest Request(Seed seed) =>
        new(
            seed.AssignmentId,
            seed.RevisionId,
            Guid.NewGuid(),
            "Completed the milestone and included supporting evidence.",
            new[]
            {
                new SubmissionEvidenceInput(
                    "Repository",
                    "https://example.com/evidence")
            });

    internal async Task<Seed> SeedAsync(TrackerDbContext db, string? candidateUserId = null, DateTimeOffset? dueAt = null)
    {
        var template = await db.Assignments.AsNoTracking()
            .SingleAsync(item => item.AssignmentId == _fixture.AssignmentId);

        var candidateId = candidateUserId ?? template.CandidateId!;
        var administratorId = template.CreatedBy;
        var now = dueAt is { } deadline && deadline <= DateTimeOffset.UtcNow ? deadline.AddDays(-1) : DateTimeOffset.UtcNow;

        var cohort = new Cohort
        {
            Name = "Submission test cohort",
            Code = $"SUB-{Guid.NewGuid():N}",
            CreatedAt = now,
            CreatedBy = administratorId
        };

        var cycle = new LearningCycle
        {
            CohortId = cohort.CohortId,
            Name = "Submission test cycle",
            StartsOn = DateOnly.FromDateTime(now.UtcDateTime),
            EndsOn = DateOnly.FromDateTime(now.UtcDateTime).AddMonths(1),
            CreatedAt = now,
            CreatedBy = administratorId
        };

        var project = new Project
        {
            CycleId = cycle.CycleId,
            Title = "Submission test project",
            Brief = "Test individual submissions",
            Mode = ProjectMode.Individual,
            CreatedAt = now,
            CreatedBy = administratorId
        };

        var milestone = new Milestone
        {
            ProjectId = project.ProjectId,
            SequenceNo = 1,
            Title = "Submission test milestone",
            Instructions = "Complete the work.",
            AcceptanceExpectations = "Provide working evidence.",
            EvidencePolicy = "Provide an HTTPS link.",
            CreatedAt = now,
            CreatedBy = administratorId
        };

        var assignment = new Assignment
        {
            CycleId = cycle.CycleId,
            ProjectId = project.ProjectId,
            MilestoneId = milestone.MilestoneId,
            CandidateId = candidateId,
            DueAt = dueAt ?? now.AddDays(7),
            CreatedAt = now,
            CreatedBy = administratorId
        };

        var revision = new AssignmentRevision
        {
            AssignmentId = assignment.AssignmentId,
            RevisionNo = 1,
            DueAt = assignment.DueAt,
            InstructionsSnapshot = milestone.Instructions,
            AcceptanceSnapshot = milestone.AcceptanceExpectations,
            EvidencePolicySnapshot = milestone.EvidencePolicy,
            ChangedAt = now,
            ChangedBy = administratorId,
            Reason = "Initial assignment"
        };

        db.Cohorts.Add(cohort);
        db.CohortMemberships.Add(new CohortMembership
        {
            CohortId = cohort.CohortId,
            UserId = candidateId,
            MembershipKind = MembershipKind.Candidate,
            StartsAt = now.AddDays(-1),
            ChangeReason = "Submission test membership",
            CreatedBy = administratorId
        });
        db.LearningCycles.Add(cycle);
        db.Projects.Add(project);
        db.Milestones.Add(milestone);
        db.Assignments.Add(assignment);
        db.AssignmentRevisions.Add(revision);

        await db.SaveChangesAsync();

        return new Seed(
            assignment.AssignmentId,
            revision.RevisionId,
            cohort.CohortId,
            project.ProjectId,
            candidateId,
            administratorId);
    }

    internal sealed record Seed(
        Guid AssignmentId,
        Guid RevisionId,
        Guid CohortId,
        Guid ProjectId,
        string CandidateId,
        string AdministratorId);
}
