using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

internal sealed class ReviewTestContext : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;
    internal TrackerDbContext Db { get; }
    internal IServiceProvider Services => _scope.ServiceProvider;
    internal IndividualSubmissionTests.Seed Seed { get; }
    internal string MentorId { get; }
    internal IIndividualSubmissionReviewService Reviews => Services.GetRequiredService<IIndividualSubmissionReviewService>();
    internal IAssignmentReopeningService Reopenings => Services.GetRequiredService<IAssignmentReopeningService>();
    internal IIndividualSubmissionService Submissions => Services.GetRequiredService<IIndividualSubmissionService>();
    internal IndividualSubmissionRequest OriginalRequest { get; }
    internal SubmissionReceipt OriginalReceipt { get; }

    private ReviewTestContext(AsyncServiceScope scope, TrackerDbContext db, IndividualSubmissionTests.Seed seed,
        string mentorId, IndividualSubmissionRequest request, SubmissionReceipt receipt)
    { _scope = scope; Db = db; Seed = seed; MentorId = mentorId; OriginalRequest = request; OriginalReceipt = receipt; }

    internal static async Task<ReviewTestContext> CreateAsync(WebAccessFixture fixture, string? candidateUserId = null, DateTimeOffset? dueAt = null)
    {
        var scope = fixture.Services.CreateAsyncScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            var seed = await new IndividualSubmissionTests(fixture).SeedAsync(db, candidateUserId, dueAt);
            // Select the fixture's one mentor, not team helpers' additional mentor accounts.
            var mentor = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                                join u in db.Users on ur.UserId equals u.Id
                                where r.NormalizedName == "MENTOR" && u.UserName!.StartsWith("web.Mentor.") select ur.UserId).SingleAsync();
            db.CohortMemberships.Add(new CohortMembership { CohortId = seed.CohortId, UserId = mentor,
                MembershipKind = MembershipKind.Mentor, StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedBy = seed.AdministratorId, ChangeReason = "Review workflow test" });
            await db.SaveChangesAsync();
            var request = IndividualSubmissionTests.Request(seed);
            var result = await scope.ServiceProvider.GetRequiredService<IIndividualSubmissionService>().SubmitAsync(request, seed.CandidateId);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
            return new(scope, db, seed, mentor, request, result.Receipt!);
        }
        catch { await scope.DisposeAsync(); throw; }
    }

    internal async Task<ReviewRequest> DecisionAsync(ReviewDecision decision = ReviewDecision.Approved, string feedback = "")
    {
        var page = await Reviews.GetReviewAsync(Seed.AssignmentId, MentorId);
        Assert.NotNull(page);
        return new(Seed.AssignmentId, page.Attempt!.ReceiptId, page.RowVersion, Guid.NewGuid(), decision, feedback);
    }

    internal async Task<WorkflowResult> ApproveAsync()
    {
        var result = await Reviews.DecideAsync(await DecisionAsync(), MentorId);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result;
    }

    internal async Task<ReopeningRequest> ReopeningAsync(string reason = "Reconsider the submitted evidence.")
    {
        var page = (await Reopenings.GetReopeningAsync(Seed.AssignmentId, Seed.AdministratorId))!;
        return new(Seed.AssignmentId, page.EffectiveApprovalId ?? Guid.Empty, page.RowVersion, Guid.NewGuid(), reason);
    }

    internal async Task ReviseAsync(string? instructions = null, string? acceptance = null,
        string? evidence = null, bool deadlineOnly = false)
    {
        var service = Services.GetRequiredService<IAssignmentRevisionService>();
        var page = (await service.GetAsync(Seed.AssignmentId, Seed.AdministratorId))!;
        var result = await service.ReviseAsync(Seed.AssignmentId, page.RowVersion,
            deadlineOnly ? page.DueAt.AddDays(1) : page.DueAt,
            instructions ?? page.Instructions, acceptance ?? page.AcceptanceExpectations,
            evidence ?? page.EvidencePolicy, "Review eligibility test", Seed.AdministratorId);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
    }

    internal async Task<IndividualSubmissionRequest> NewRequestAsync()
    {
        var revisionId = await Db.AssignmentRevisions.Where(r => r.AssignmentId == Seed.AssignmentId)
            .OrderByDescending(r => r.RevisionNo).Select(r => r.RevisionId).FirstAsync();
        return OriginalRequest with { AssignmentRevisionId = revisionId, RequestId = Guid.NewGuid() };
    }

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
}
