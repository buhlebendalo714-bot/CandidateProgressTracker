using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class SubmissionLockTimeoutTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_lock_command_timeout_is_retryable_without_partial_writes(bool team)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed = team ? t.Seed : await new IndividualSubmissionTests(fixture).SeedAsync(db);
        var teamRequest = team ? await t.RequestAsync() : null;
        var request = teamRequest?.Content ?? IndividualSubmissionTests.Request(seed);
        await using var blockerScope = fixture.Services.CreateAsyncScope();
        var blocker = blockerScope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        await using (var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var resource = $"CandidateProgressTracker:Cohort:{seed.CohortId:N}";
            await blocker.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction';");
            // The lock acquisition above coordinates the requests. A provider command
            // timeout, not a sleep, deterministically exercises the SQL -2 path.
            db.Database.SetCommandTimeout(1);
            var result = team
                ? await new TeamSubmissionService(db).SubmitAsync(teamRequest!, seed.CandidateId)
                : await new IndividualSubmissionService(db).SubmitAsync(request, seed.CandidateId);
            Assert.False(result.Succeeded); Assert.Null(result.Receipt);
            Assert.Contains(result.Errors, e => e.Contains("Retry", StringComparison.OrdinalIgnoreCase));
            Assert.False(await blocker.Submissions.AnyAsync(s => s.AssignmentId == seed.AssignmentId));
            if (team) Assert.False(await blocker.AssignmentCreditedRosters.AnyAsync(r => r.AssignmentId == seed.AssignmentId));
            await transaction.CommitAsync();
        }
        db.Database.SetCommandTimeout(30);
        var retry = team
            ? await new TeamSubmissionService(db).SubmitAsync(teamRequest!, seed.CandidateId)
            : await new IndividualSubmissionService(db).SubmitAsync(request, seed.CandidateId);
        Assert.True(retry.Succeeded, string.Join("; ", retry.Errors)); Assert.Equal(1, retry.Receipt!.AttemptNo);
        Assert.Single(await db.Submissions.Where(s => s.AssignmentId == seed.AssignmentId).ToListAsync());
    }
}
