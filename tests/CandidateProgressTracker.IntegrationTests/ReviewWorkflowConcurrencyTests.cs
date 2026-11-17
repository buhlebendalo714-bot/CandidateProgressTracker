using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class ReviewWorkflowConcurrencyTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData("duplicate-review")]
    [InlineData("conflicting-review")]
    [InlineData("submit-review")]
    [InlineData("revise-review")]
    [InlineData("duplicate-reopening")]
    [InlineData("conflicting-reopening")]
    [InlineData("submit-reopening")]
    [InlineData("revise-reopening")]
    public async Task Workflow_writes_serialize_and_preserve_receipts_and_versions(string scenario)
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        if (scenario.Contains("reopening")) await t.ApproveAsync();
        var decision = await t.DecisionAsync();
        var reopening = await t.ReopeningAsync();
        var newSubmission = await t.NewRequestAsync();
        var revision = (await t.Services.GetRequiredService<IAssignmentRevisionService>().GetAsync(t.Seed.AssignmentId, t.Seed.AdministratorId))!;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contexts = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var arrived = 0;
        async Task<(bool Success, Guid? Receipt)> WorkerAsync(bool second)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            contexts.Add(services.GetRequiredService<TrackerDbContext>().ContextId.InstanceId);
            if (Interlocked.Increment(ref arrived) == 2) ready.SetResult();
            await start.Task;
            if (second && scenario.StartsWith("submit-"))
            {
                var result = await services.GetRequiredService<IIndividualSubmissionService>().SubmitAsync(newSubmission, t.Seed.CandidateId);
                return (result.Succeeded, result.Receipt?.SubmissionId);
            }
            if (second && scenario.StartsWith("revise-"))
            {
                var result = await services.GetRequiredService<IAssignmentRevisionService>().ReviseAsync(t.Seed.AssignmentId,
                    revision.RowVersion, revision.DueAt.AddDays(1), revision.Instructions,
                    revision.AcceptanceExpectations, revision.EvidencePolicy, "Concurrent revision", t.Seed.AdministratorId);
                return (result.Succeeded, null);
            }
            var service = services.GetRequiredService<IIndividualSubmissionReviewService>();
            var workflowResult = scenario.Contains("reopening")
                ? await services.GetRequiredService<IAssignmentReopeningService>().ReopenAsync(
                    second && scenario == "conflicting-reopening" ? reopening with { RequestId = Guid.NewGuid() } : reopening, t.Seed.AdministratorId)
                : await service.DecideAsync(second && scenario == "conflicting-review"
                    ? decision with { RequestId = Guid.NewGuid(), Decision = ReviewDecision.ChangesRequested, Feedback = "Please revise" } : decision, t.MentorId);
            return (workflowResult.Succeeded, workflowResult.ReceiptId);
        }
        var tasks = new[] { WorkerAsync(false), WorkerAsync(true) };
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        start.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(90));
        Assert.Equal(2, contexts.Distinct().Count());
        Assert.Contains(results, r => r.Success);
        if (scenario.StartsWith("duplicate-"))
        {
            Assert.All(results, r => Assert.True(r.Success));
            Assert.Equal(results[0].Receipt, results[1].Receipt);
        }
        if (scenario.StartsWith("conflicting-") || scenario.StartsWith("revise-"))
            Assert.Single(results, r => r.Success);
        var reviews = await t.Db.SubmissionReviews.AsNoTracking().Where(r => r.AssignmentId == t.Seed.AssignmentId).ToListAsync();
        Assert.Equal(reviews.Count, reviews.Select(r => r.ReviewNo).Distinct().Count());
        Assert.True(reviews.Count <= 1);
        var attempts = await t.Db.Submissions.AsNoTracking().Where(s => s.AssignmentId == t.Seed.AssignmentId).ToListAsync();
        Assert.Equal(attempts.Count, attempts.Select(s => s.AttemptNo).Distinct().Count());
        Assert.All(reviews, r => Assert.Contains(attempts, s => s.SubmissionId == r.SubmissionId));
        if (scenario == "submit-review") Assert.True(attempts.Count == 1 || reviews.Count == 0);
        if (scenario.Contains("reopening"))
            Assert.InRange(await t.Db.AssignmentReopenings.CountAsync(r => r.AssignmentId == t.Seed.AssignmentId), 0, 1);
        // Accepted requests remain idempotent even after their form token becomes stale.
        if (results[0].Success)
        {
            var retry = scenario.Contains("reopening")
                ? await t.Reopenings.ReopenAsync(reopening, t.Seed.AdministratorId)
                : await t.Reviews.DecideAsync(decision, t.MentorId);
            Assert.True(retry.Succeeded, string.Join("; ", retry.Errors));
            Assert.Equal(results[0].Receipt, retry.ReceiptId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rollback_preserves_version_and_audit_and_allows_identical_retry(bool reopening)
    {
        await using var t = await ReviewTestContext.CreateAsync(fixture);
        if (reopening) await t.ApproveAsync();
        var decision = await t.DecisionAsync();
        var reopen = await t.ReopeningAsync();
        var version = await t.Db.Assignments.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.RowVersion).SingleAsync();
        var reviewCount = await t.Db.SubmissionReviews.CountAsync();
        var options = new DbContextOptionsBuilder<TrackerDbContext>(t.Services.GetRequiredService<DbContextOptions<TrackerDbContext>>())
            .AddInterceptors(new FailAfterSave()).Options;
        await using var db = new TrackerDbContext(options);
        var service = new IndividualSubmissionReviewService(db);
        await Assert.ThrowsAsync<InjectedFailure>(async () =>
        {
            if (reopening) await service.ReopenAsync(reopen, t.Seed.AdministratorId);
            else await service.DecideAsync(decision, t.MentorId);
        });
        Assert.Equal(version, await t.Db.Assignments.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.RowVersion).SingleAsync());
        Assert.Equal(reviewCount, await t.Db.SubmissionReviews.CountAsync());
        Assert.False(await t.Db.AssignmentReopenings.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.Entity is SubmissionReview or AssignmentReopening);
        var retry = reopening ? await service.ReopenAsync(reopen, t.Seed.AdministratorId) : await service.DecideAsync(decision, t.MentorId);
        Assert.True(retry.Succeeded, string.Join("; ", retry.Errors));
    }

    private sealed class InjectedFailure : Exception;
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        private bool _failed;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!_failed) { _failed = true; throw new InjectedFailure(); }
            return ValueTask.FromResult(result);
        }
    }
}
