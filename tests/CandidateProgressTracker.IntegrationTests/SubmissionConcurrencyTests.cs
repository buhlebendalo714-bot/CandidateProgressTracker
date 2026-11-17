using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using CandidateProgressTracker.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class SubmissionConcurrencyTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_requests_preserve_idempotency_attempt_numbers_and_evidence(bool identical)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var original = IndividualSubmissionTests.Request(seed) with
        {
            Evidence = new[] { new SubmissionEvidenceInput("Repository", "https://example.com/repository"),
                new SubmissionEvidenceInput("Demo", "https://example.com/demo") }
        };
        var requests = Enumerable.Range(0, 4).Select(_ => identical ? original : original with { RequestId = Guid.NewGuid() }).ToArray();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var contextIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        async Task<SubmissionResult> WorkerAsync(IndividualSubmissionRequest request)
        {
            await using var workerScope = fixture.Services.CreateAsyncScope();
            var workerDb = workerScope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            var service = workerScope.ServiceProvider.GetRequiredService<IIndividualSubmissionService>();
            contextIds.Add(workerDb.ContextId.InstanceId);
            if (Interlocked.Increment(ref arrived) == requests.Length) ready.SetResult();
            await start.Task;
            var result = await service.SubmitAsync(request, seed.CandidateId);
            // Failed attempts must not leave tracked inserts that a later SaveChanges could persist.
            Assert.DoesNotContain(workerDb.ChangeTracker.Entries(), e =>
                e.Entity is Submission or SubmissionEvidence && e.State == EntityState.Added);
            return result;
        }
        var tasks = requests.Select(WorkerAsync).ToArray();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        start.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(90));
        Assert.Equal(requests.Length, contextIds.Distinct().Count());
        Assert.Contains(results, result => result.Succeeded);
        foreach (var result in results.Where(result => !result.Succeeded))
        {
            Assert.Null(result.Receipt);
            Assert.Contains("Another request changed the submission records. Retry using the same request ID.", result.Errors);
        }

        var initialRows = await db.Submissions.AsNoTracking().Where(s => s.AssignmentId == seed.AssignmentId).ToListAsync();
        Assert.Equal(identical ? 1 : results.Count(r => r.Succeeded), initialRows.Count);
        Assert.Equal(initialRows.Count, initialRows.Select(s => s.AttemptNo).Distinct().Count());
        var initialIds = initialRows.Select(s => s.SubmissionId).ToArray();
        Assert.Equal(initialRows.Count * 2, await db.SubmissionEvidence.CountAsync(e => initialIds.Contains(e.SubmissionId)));

        // Retry serially, with the original request IDs, including requests that already succeeded.
        var receipts = new List<SubmissionReceipt>();
        for (var i = 0; i < requests.Length; i++)
        {
            await using var retryScope = fixture.Services.CreateAsyncScope();
            var retry = await retryScope.ServiceProvider.GetRequiredService<IIndividualSubmissionService>()
                .SubmitAsync(requests[i], seed.CandidateId);
            Assert.True(retry.Succeeded, string.Join("; ", retry.Errors));
            Assert.NotNull(retry.Receipt);
            if (results[i].Succeeded) Assert.Equal(results[i].Receipt, retry.Receipt);
            receipts.Add(retry.Receipt);
        }
        var finalRows = await db.Submissions.AsNoTracking().Where(s => s.AssignmentId == seed.AssignmentId).OrderBy(s => s.AttemptNo).ToListAsync();
        var expectedCount = identical ? 1 : requests.Length;
        Assert.Equal(expectedCount, finalRows.Count);
        Assert.Equal(Enumerable.Range(1, expectedCount), finalRows.Select(s => s.AttemptNo));
        Assert.Equal(expectedCount, receipts.Distinct().Count());
        Assert.All(finalRows, s => Assert.Equal(seed.RevisionId, s.AssignmentRevisionId));
        foreach (var saved in finalRows)
        {
            var evidence = await db.SubmissionEvidence.AsNoTracking().Where(e => e.SubmissionId == saved.SubmissionId)
                .OrderBy(e => e.SequenceNo).ToListAsync();
            Assert.Equal(original.Evidence, evidence.Select(e => new SubmissionEvidenceInput(e.Label, e.Url)));
        }
    }

    [Fact]
    public async Task Failure_after_database_writes_rolls_back_everything_and_same_context_can_retry()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var evidenceBefore = await db.SubmissionEvidence.CountAsync();
        var options = new DbContextOptionsBuilder<TrackerDbContext>(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<TrackerDbContext>>())
            .AddInterceptors(new FailAfterSubmissionSave()).Options;
        await using var failingDb = new TrackerDbContext(options);
        var service = new IndividualSubmissionService(failingDb);
        var request = IndividualSubmissionTests.Request(seed);
        await Assert.ThrowsAsync<InjectedSaveFailure>(() => service.SubmitAsync(request, seed.CandidateId));
        Assert.Empty(failingDb.ChangeTracker.Entries<Submission>());
        Assert.Empty(failingDb.ChangeTracker.Entries<SubmissionEvidence>());
        Assert.False(await db.Submissions.AnyAsync(s => s.AssignmentId == seed.AssignmentId));
        Assert.Equal(evidenceBefore, await db.SubmissionEvidence.CountAsync());
        var retry = await service.SubmitAsync(request, seed.CandidateId);
        Assert.True(retry.Succeeded, string.Join("; ", retry.Errors));
        Assert.Equal(1, retry.Receipt!.AttemptNo);
        Assert.Single(await db.Submissions.Where(s => s.AssignmentId == seed.AssignmentId).ToListAsync());
        Assert.Equal(evidenceBefore + 1, await db.SubmissionEvidence.CountAsync());
    }

    private sealed class InjectedSaveFailure : Exception;

    private sealed class FailAfterSubmissionSave : SaveChangesInterceptor
    {
        private bool _failed;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<Submission>().Any())
            {
                _failed = true;
                throw new InjectedSaveFailure(); // Actual INSERTs completed; the outer transaction has not committed.
            }
            return ValueTask.FromResult(result);
        }
    }
}
