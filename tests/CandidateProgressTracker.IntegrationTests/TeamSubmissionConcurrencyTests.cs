using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class TeamSubmissionConcurrencyTests(WebAccessFixture fixture) : IClassFixture<WebAccessFixture>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_first_attempts_freeze_once_preserve_retry_receipts_and_allocate_unique_attempt_numbers(bool identical)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var request = await t.RequestAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contexts = new System.Collections.Concurrent.ConcurrentBag<Guid>(); var arrived = 0;
        var inputs = Enumerable.Range(0, 4).Select(i => (Actor: identical || i % 2 == 0 ? t.Seed.CandidateId : t.OtherId,
            Request: identical ? request : request with { Content = request.Content with { RequestId = Guid.NewGuid() } })).ToArray();
        async Task<SubmissionResult> Worker((string Actor, TeamSubmissionRequest Request) input)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            contexts.Add(scope.ServiceProvider.GetRequiredService<TrackerDbContext>().ContextId.InstanceId);
            if (Interlocked.Increment(ref arrived) == inputs.Length) ready.SetResult(); await start.Task;
            return await scope.ServiceProvider.GetRequiredService<ITeamSubmissionService>().SubmitAsync(input.Request, input.Actor);
        }
        var tasks = inputs.Select(Worker).ToArray(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(30)); start.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(90));
        Assert.Equal(4, contexts.Distinct().Count()); Assert.Contains(results, r => r.Succeeded);
        for (var i = 0; i < inputs.Length; i++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var retry = await scope.ServiceProvider.GetRequiredService<ITeamSubmissionService>().SubmitAsync(inputs[i].Request, inputs[i].Actor);
            Assert.True(retry.Succeeded, string.Join("; ", retry.Errors));
            if (results[i].Succeeded) Assert.Equal(results[i].Receipt, retry.Receipt);
        }
        var attempts = await t.Db.Submissions.Where(s => s.AssignmentId == t.Seed.AssignmentId).OrderBy(s => s.AttemptNo).ToListAsync();
        Assert.Equal(Enumerable.Range(1, identical ? 1 : 4), attempts.Select(s => s.AttemptNo));
        Assert.Single(await t.Db.AssignmentCreditedRosters.Where(r => r.AssignmentId == t.Seed.AssignmentId).ToListAsync());
        Assert.Equal(2, await t.Db.AssignmentCreditedRosterMembers.CountAsync(m => m.AssignmentId == t.Seed.AssignmentId));
        Assert.All(attempts, s => Assert.Equal(t.Seed.AssignmentId, s.CreditedRosterAssignmentId));
        var ids = attempts.Select(s => s.SubmissionId).ToArray();
        Assert.Equal(attempts.Count, await t.Db.SubmissionEvidence.CountAsync(e => ids.Contains(e.SubmissionId)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_after_roster_or_attempt_writes_rolls_back_all_records_and_version(bool afterAttempt)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var request = await t.RequestAsync();
        var version = await t.Db.Assignments.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.RowVersion).SingleAsync();
        var evidence = await t.Db.SubmissionEvidence.CountAsync();
        var options = new DbContextOptionsBuilder<TrackerDbContext>(t.Services.GetRequiredService<DbContextOptions<TrackerDbContext>>()).AddInterceptors(new FailSave(afterAttempt)).Options;
        await using var db = new TrackerDbContext(options); var service = new TeamSubmissionService(db);
        await Assert.ThrowsAsync<InjectedFailure>(() => service.SubmitAsync(request, t.Seed.CandidateId));
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.Entity is AssignmentCreditedRoster or AssignmentCreditedRosterMember or Submission or SubmissionEvidence);
        Assert.False(await t.Db.AssignmentCreditedRosters.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.False(await t.Db.AssignmentCreditedRosterMembers.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.False(await t.Db.Submissions.AnyAsync(r => r.AssignmentId == t.Seed.AssignmentId));
        Assert.Equal(evidence, await t.Db.SubmissionEvidence.CountAsync());
        Assert.Equal(version, await t.Db.Assignments.AsNoTracking().Where(a => a.AssignmentId == t.Seed.AssignmentId).Select(a => a.RowVersion).SingleAsync());
        Assert.True((await service.SubmitAsync(request, t.Seed.CandidateId)).Succeeded);
    }
    [Theory]
    [InlineData("leave")]
    [InlineData("join")]
    [InlineData("role")]
    public async Task First_freeze_and_membership_or_role_mutation_have_one_consistent_outcome(string mutation)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture);
        var joiner = await t.AddUserAsync(team: false);
        var request = await t.RequestAsync();
        var membership = await t.Db.TeamMemberships.AsNoTracking().SingleAsync(m => m.ProjectId == t.Seed.ProjectId && m.UserId == t.OtherId);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrived = 0;
        var contexts = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        async Task<T> Run<T>(Func<IServiceProvider, Task<T>> action)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            contexts.Add(scope.ServiceProvider.GetRequiredService<TrackerDbContext>().ContextId.InstanceId);
            if (Interlocked.Increment(ref arrived) == 2) ready.SetResult(); await start.Task;
            return await action(scope.ServiceProvider);
        }
        var submit = Run(s => s.GetRequiredService<ITeamSubmissionService>().SubmitAsync(request, t.Seed.CandidateId));
        var mutate = Run(async s =>
        {
            if (mutation == "role")
            {
                var users = s.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
                var result = await users.RemoveFromRoleAsync((await users.FindByIdAsync(t.OtherId))!, "Candidate");
                Assert.True(result.Succeeded); return true;
            }
            var teams = s.GetRequiredService<IProjectTeamService>();
            var result2 = mutation == "join"
                ? await teams.AssignAsync(t.Seed.ProjectId, t.TeamId, joiner, "Concurrent join", t.Seed.AdministratorId)
                : await teams.EndAsync(t.Seed.ProjectId, membership.TeamMembershipId, Convert.ToBase64String(membership.RowVersion), "Concurrent leave", t.Seed.AdministratorId);
            Assert.True(result2.Succeeded, string.Join("; ", result2.Errors)); return true;
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30)); start.SetResult();
        await Task.WhenAll(submit, mutate).WaitAsync(TimeSpan.FromSeconds(90)); Assert.Equal(2, contexts.Distinct().Count());
        var members = await t.Db.AssignmentCreditedRosterMembers.AsNoTracking().Where(m => m.AssignmentId == t.Seed.AssignmentId).ToListAsync();
        var submitted = await submit;
        if (submitted.Succeeded)
        {
            Assert.Equal(2, members.Count); Assert.Contains(members, m => m.UserId == t.OtherId); Assert.DoesNotContain(members, m => m.UserId == joiner);
            Assert.Equal(submitted.Receipt, (await t.Teams.SubmitAsync(request, t.Seed.CandidateId)).Receipt);
        }
        else
        {
            Assert.Empty(members); Assert.False(await t.Db.Submissions.AnyAsync(s => s.AssignmentId == t.Seed.AssignmentId));
            await t.SubmitAsync();
            members = await t.Db.AssignmentCreditedRosterMembers.Where(m => m.AssignmentId == t.Seed.AssignmentId).ToListAsync();
            Assert.Equal(mutation == "join" ? 3 : 1, members.Count);
        }
    }

    [Theory]
    [InlineData("duplicate-review")]
    [InlineData("conflicting-review")]
    [InlineData("submit-review")]
    [InlineData("revise-review")]
    [InlineData("duplicate-reopening")]
    [InlineData("conflicting-reopening")]
    [InlineData("submit-reopening")]
    [InlineData("revise-reopening")]
    public async Task Shared_team_workflow_concurrency_preserves_roster_numbering_tokens_and_idempotency(string scenario)
    {
        await using var t = await TeamTestContext.CreateAsync(fixture); await t.SubmitAsync();
        if (scenario.Contains("reopening")) Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(), t.MentorId)).Succeeded);
        var decision = await t.DecisionAsync(); var reopening = await t.ReopeningAsync(); var request = await t.RequestAsync(t.OtherId);
        var revision = (await t.Services.GetRequiredService<IAssignmentRevisionService>().GetAsync(t.Seed.AssignmentId, t.Seed.AdministratorId))!;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrived = 0;
        async Task<(bool Success, Guid? Receipt)> Worker(bool second)
        {
            await using var scope = fixture.Services.CreateAsyncScope(); var s = scope.ServiceProvider;
            if (Interlocked.Increment(ref arrived) == 2) ready.SetResult(); await start.Task;
            if (second && scenario.StartsWith("submit-"))
            { var r = await s.GetRequiredService<ITeamSubmissionService>().SubmitAsync(request, t.OtherId); return (r.Succeeded, r.Receipt?.SubmissionId); }
            if (second && scenario.StartsWith("revise-"))
            { var r = await s.GetRequiredService<IAssignmentRevisionService>().ReviseAsync(t.Seed.AssignmentId, revision.RowVersion,
                revision.DueAt.AddDays(1), revision.Instructions, revision.AcceptanceExpectations, revision.EvidencePolicy, "Concurrent revision", t.Seed.AdministratorId); return (r.Succeeded, null); }
            var result = scenario.Contains("reopening")
                ? await s.GetRequiredService<IAssignmentReopeningService>().ReopenAsync(second && scenario.StartsWith("conflicting-") ? reopening with { RequestId = Guid.NewGuid() } : reopening, t.Seed.AdministratorId)
                : await s.GetRequiredService<IIndividualSubmissionReviewService>().DecideAsync(second && scenario.StartsWith("conflicting-") ? decision with { RequestId = Guid.NewGuid() } : decision, t.MentorId);
            return (result.Succeeded, result.ReceiptId);
        }
        var tasks = new[] { Worker(false), Worker(true) }; await ready.Task.WaitAsync(TimeSpan.FromSeconds(30)); start.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(90)); Assert.Contains(results, r => r.Success);
        if (scenario.StartsWith("duplicate-")) { Assert.All(results, r => Assert.True(r.Success)); Assert.Equal(results[0].Receipt, results[1].Receipt); }
        if (scenario.StartsWith("conflicting-") || scenario.StartsWith("revise-")) Assert.Single(results, r => r.Success);
        if (results[0].Success)
        {
            var retry = scenario.Contains("reopening") ? await t.Reopenings.ReopenAsync(reopening, t.Seed.AdministratorId) : await t.Reviews.DecideAsync(decision, t.MentorId);
            Assert.Equal(results[0].Receipt, retry.ReceiptId);
        }
        var attempts = await t.Db.Submissions.AsNoTracking().Where(s => s.AssignmentId == t.Seed.AssignmentId).ToListAsync();
        Assert.Equal(attempts.Count, attempts.Select(a => a.AttemptNo).Distinct().Count());
        Assert.Equal(2, await t.Db.AssignmentCreditedRosterMembers.CountAsync(m => m.AssignmentId == t.Seed.AssignmentId));
        var reviews = await t.Db.SubmissionReviews.Where(r => r.AssignmentId == t.Seed.AssignmentId).ToListAsync();
        Assert.Equal(reviews.Count, reviews.Select(r => r.ReviewNo).Distinct().Count());
        if (scenario == "submit-review") Assert.True(attempts.Count == 1 || reviews.Count == 0);
    }

    private sealed class InjectedFailure : Exception;
    private sealed class FailSave(bool afterAttempt) : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!failed && (!afterAttempt || eventData.Context!.ChangeTracker.Entries<Submission>().Any())) { failed = true; throw new InjectedFailure(); }
            return ValueTask.FromResult(result);
        }
    }
}
