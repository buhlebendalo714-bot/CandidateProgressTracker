using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class ReviewMigrationUpgradeTests
{
    private const string Predecessor = "20261105173435_AddAssignmentSubmissions";
    private const string Target = "20261106175348_AddIndividualSubmissionReviews";

    [Fact]
    public async Task Upgrade_preserves_populated_predecessor_and_enforces_new_audit_constraints()
    {
        // This factory always generates CandidateProgressTracker_Tests_<unique GUID>.
        // It never reads the application's development connection string.
        await using var database = await TestDatabase.CreateAsync(Predecessor);
        await using var scope = database.Services.CreateAsyncScope();
        await using var db = new PreTeamDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<TrackerDbContext>>());
        var current = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        Assert.Null(db.Model.FindEntityType(typeof(Submission))!.FindProperty(nameof(Submission.CreditedRosterAssignmentId)));
        var catalog = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
        Assert.StartsWith("CandidateProgressTracker_Tests_", catalog);
        Assert.True(Guid.TryParseExact(catalog["CandidateProgressTracker_Tests_".Length..], "N", out _));
        Assert.Equal(Predecessor, (await db.Database.GetAppliedMigrationsAsync()).Last());
        var migrations = current.Database.GetMigrations().ToList();
        Assert.Equal(Predecessor, migrations[migrations.IndexOf(Target) - 1]);
        Assert.Contains(Target, await current.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await AuditTableCountAsync(db));
        var seed = await SeedAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        var before = await SnapshotAsync(db);
        Assert.Equal(2, await db.Assignments.CountAsync());
        Assert.Equal(4, await db.AssignmentRevisions.CountAsync());
        Assert.Equal(3, await db.Submissions.CountAsync());
        Assert.Equal(6, await db.SubmissionEvidence.CountAsync());
        db.ChangeTracker.Clear();

        await scope.ServiceProvider.GetRequiredService<TrackerDbContext>().GetService<IMigrator>().MigrateAsync(Target);

        Assert.Equal(Target, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.DoesNotContain(Target, await current.Database.GetPendingMigrationsAsync());
        Assert.Equal(2, await AuditTableCountAsync(db));
        await AssertUnchangedAsync(db, before);
        Assert.Empty(await db.SubmissionReviews.ToListAsync());
        Assert.Empty(await db.AssignmentReopenings.ToListAsync());

        // Existing attempts can be referenced without rewriting or copying their revisions.
        var first = NewReview(seed.First, seed.MentorId, 1);
        var second = NewReview(seed.Second, seed.MentorId, 1);
        db.SubmissionReviews.AddRange(first, second);
        await db.SaveChangesAsync();
        var opened = NewReopening(first, seed.AdministratorId);
        db.AssignmentReopenings.Add(opened);
        await db.SaveChangesAsync();
        var referenced = await (from reopening in db.AssignmentReopenings
                                join review in db.SubmissionReviews on reopening.ApprovalReviewId equals review.ReviewId
                                join submission in db.Submissions on review.SubmissionId equals submission.SubmissionId
                                join revision in db.AssignmentRevisions on submission.AssignmentRevisionId equals revision.RevisionId
                                where reopening.ReopeningId == opened.ReopeningId
                                select new { submission.SubmissionId, revision.RevisionId }).SingleAsync();
        Assert.Equal(seed.First.SubmissionId, referenced.SubmissionId);
        Assert.Equal(seed.First.AssignmentRevisionId, referenced.RevisionId);

        var reviewFailures = new (string Name, Action<SubmissionReview> Mutate, int[] Errors)[]
        {
            ("assignment-scoped submission FK", r => r.AssignmentId = seed.Second.AssignmentId, new[] { 547 }),
            ("missing submission", r => r.SubmissionId = Guid.NewGuid(), new[] { 547 }),
            ("reviewer FK", r => r.ReviewerId = "missing-user", new[] { 547 }),
            ("decision values", r => r.Decision = (ReviewDecision)99, new[] { 547 }),
            ("required feedback", r => { r.Decision = ReviewDecision.ChangesRequested; r.Feedback = "   "; }, new[] { 547 }),
            ("feedback limit", r => r.Feedback = new string('x', 10001), new[] { 547 }),
            ("positive numbering", r => r.ReviewNo = 0, new[] { 547 }),
            ("nonempty request", r => r.RequestId = Guid.Empty, new[] { 547 }),
            ("UTC timestamp", r => r.DecidedAt = r.DecidedAt.ToOffset(TimeSpan.FromHours(2)), new[] { 547 }),
            ("unique numbering", r => r.ReviewNo = first.ReviewNo, new[] { 2601, 2627 }),
            ("idempotency key", r => r.RequestId = first.RequestId, new[] { 2601, 2627 })
        };
        foreach (var failure in reviewFailures)
        {
            var invalid = NewReview(seed.First, seed.MentorId, 2);
            failure.Mutate(invalid);
            await RejectAsync(db, invalid, failure.Name, failure.Errors);
        }
        var reopeningFailures = new (string Name, Action<AssignmentReopening> Mutate, int[] Errors)[]
        {
            ("assignment-scoped approval FK", r => r.AssignmentId = seed.First.AssignmentId, new[] { 547 }),
            ("missing approval", r => r.ApprovalReviewId = Guid.NewGuid(), new[] { 547 }),
            ("administrator FK", r => r.AdministratorId = "missing-user", new[] { 547 }),
            ("required reason", r => r.Reason = "   ", new[] { 547 }),
            ("reason limit", r => r.Reason = new string('x', 1001), new[] { 2628, 8152 }),
            ("nonempty reopening request", r => r.RequestId = Guid.Empty, new[] { 547 }),
            ("UTC reopening timestamp", r => r.ReopenedAt = r.ReopenedAt.ToOffset(TimeSpan.FromHours(2)), new[] { 547 }),
            ("one reopening per approval", r => { r.ApprovalReviewId = first.ReviewId; r.AssignmentId = first.AssignmentId; }, new[] { 2601, 2627 }),
            ("reopening idempotency key", r => r.RequestId = opened.RequestId, new[] { 2601, 2627 })
        };
        foreach (var failure in reopeningFailures)
        {
            var invalid = NewReopening(second, seed.AdministratorId);
            failure.Mutate(invalid);
            await RejectAsync(db, invalid, failure.Name, failure.Errors);
        }
        var deleteReview = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [SubmissionReviews] WHERE [ReviewId] = {first.ReviewId}"));
        Assert.Equal(547, deleteReview.Number);
        var deleteSubmission = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [Submissions] WHERE [SubmissionId] = {seed.First.SubmissionId}"));
        Assert.Equal(547, deleteSubmission.Number);
        Assert.Equal(2, await db.SubmissionReviews.CountAsync());
        Assert.Equal(1, await db.AssignmentReopenings.CountAsync());
        await AssertUnchangedAsync(db, before);
    }

    private static Task<int> AuditTableCountAsync(TrackerDbContext db) => db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE [name] IN ('SubmissionReviews', 'AssignmentReopenings')").SingleAsync();

    private static async Task RejectAsync(TrackerDbContext db, object entity, string constraint, int[] expectedErrors)
    {
        db.Add(entity);
        try
        {
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var sql = Assert.IsType<SqlException>(error.InnerException);
            Assert.True(expectedErrors.Contains(sql.Number), $"{constraint}: unexpected SQL error {sql.Number}: {sql.Message}");
        }
        finally { db.Entry(entity).State = EntityState.Detached; }
    }

    private static SubmissionReview NewReview(Submission submission, string mentorId, int number) => new()
    {
        AssignmentId = submission.AssignmentId, SubmissionId = submission.SubmissionId, ReviewNo = number,
        Decision = ReviewDecision.Approved, Feedback = "", ReviewerId = mentorId,
        DecidedAt = DateTimeOffset.UtcNow, RequestId = Guid.NewGuid()
    };
    private static AssignmentReopening NewReopening(SubmissionReview review, string administratorId) => new()
    {
        AssignmentId = review.AssignmentId, ApprovalReviewId = review.ReviewId, AdministratorId = administratorId,
        Reason = "Upgrade constraint verification", ReopenedAt = DateTimeOffset.UtcNow, RequestId = Guid.NewGuid()
    };

    internal static async Task<Dictionary<string, string>> SnapshotAsync(TrackerDbContext db)
    {
        // Capture every stored column, including rowversions, audit fields and exact
        // datetimeoffset values, rather than comparing selected DTO properties.
        var tables = new (string Table, string Order)[]
        {
            ("AspNetUsers", "[Id]"), ("AspNetRoles", "[Id]"), ("AspNetUserRoles", "[UserId], [RoleId]"),
            ("Cohorts", "[CohortId]"), ("CohortMemberships", "[MembershipId]"),
            ("LearningCycles", "[CycleId]"), ("Projects", "[ProjectId]"), ("Milestones", "[MilestoneId]"),
            ("Assignments", "[AssignmentId]"), ("AssignmentRevisions", "[RevisionId]"),
            ("Submissions", "[SubmissionId]"), ("SubmissionEvidence", "[SubmissionEvidenceId]")
        };
        var snapshot = new Dictionary<string, string>();
        foreach (var table in tables)
        {
            // Identifiers come exclusively from the fixed list above.
#pragma warning disable EF1002 // SQL identifiers cannot be parameters; the allowlist contains no external input.
            snapshot[table.Table] = await db.Database.SqlQueryRaw<string>(
                $"SELECT (SELECT * FROM [dbo].[{table.Table}] ORDER BY {table.Order} FOR JSON PATH, INCLUDE_NULL_VALUES) AS [Value]").SingleAsync();
#pragma warning restore EF1002
        }
        return snapshot;
    }
    private static async Task AssertUnchangedAsync(TrackerDbContext db, Dictionary<string, string> before)
    {
        var after = await SnapshotAsync(db);
        foreach (var table in before.Keys) Assert.Equal(before[table], after[table]);
    }

    internal static async Task<Seed> SeedAsync(TrackerDbContext db, UserManager<IdentityUser> users)
    {
        var ids = new Dictionary<string, string>();
        foreach (var role in new[] { "Administrator", "Candidate", "Mentor" })
        {
            var user = new IdentityUser { UserName = $"upgrade.{role}" };
            Assert.True((await users.CreateAsync(user, "Integration-Only!482")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            ids[role] = user.Id;
        }
        var administrator = ids["Administrator"];
        var now = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var cohort = new Cohort { Name = "Existing cohort", Code = "UPGRADE", CreatedAt = now, CreatedBy = administrator };
        var cycle = new LearningCycle { CohortId = cohort.CohortId, Name = "Existing cycle",
            StartsOn = new(2026, 11, 1), EndsOn = new(2027, 1, 31), CreatedAt = now, CreatedBy = administrator };
        var project = new Project { CycleId = cycle.CycleId, Title = "Existing individual project", Brief = "Keep this exact brief.",
            Mode = ProjectMode.Individual, CreatedAt = now, CreatedBy = administrator };
        db.AddRange(cohort, cycle, project);
        foreach (var kind in new[] { MembershipKind.Candidate, MembershipKind.Mentor })
            db.CohortMemberships.Add(new() { CohortId = cohort.CohortId, UserId = ids[kind.ToString()], MembershipKind = kind,
                StartsAt = now, CreatedBy = administrator, ChangeReason = "Existing membership audit" });
        var submissions = new List<Submission>();
        for (var assignmentNo = 1; assignmentNo <= 2; assignmentNo++)
        {
            var milestone = new Milestone { ProjectId = project.ProjectId, SequenceNo = assignmentNo, Title = $"Milestone {assignmentNo}",
                Instructions = "Current milestone text", AcceptanceExpectations = "Current expectations", EvidencePolicy = "Current policy",
                CreatedAt = now, CreatedBy = administrator };
            var assignment = new Assignment { CycleId = cycle.CycleId, ProjectId = project.ProjectId, MilestoneId = milestone.MilestoneId,
                CandidateId = ids["Candidate"], IsRequired = assignmentNo == 1, DueAt = now.AddDays(4), CreatedAt = now, CreatedBy = administrator };
            db.AddRange(milestone, assignment);
            for (var revisionNo = 1; revisionNo <= 2; revisionNo++)
            {
                var revision = new AssignmentRevision { AssignmentId = assignment.AssignmentId, RevisionNo = revisionNo,
                    DueAt = now.AddDays(revisionNo + 2), InstructionsSnapshot = $"Saved instructions v{revisionNo}",
                    AcceptanceSnapshot = $"Saved acceptance v{revisionNo}", EvidencePolicySnapshot = $"Saved evidence policy v{revisionNo}",
                    ChangedAt = now.AddHours(revisionNo), ChangedBy = administrator, Reason = $"Existing revision {revisionNo} audit" };
                db.AssignmentRevisions.Add(revision);
                if (assignmentNo == 2 && revisionNo == 2) continue;
                var received = revision.DueAt.AddTicks(revisionNo == 1 ? -1 : 1); // On-time and late historical work.
                var submission = new Submission { AssignmentId = assignment.AssignmentId, AssignmentRevisionId = revision.RevisionId,
                    AttemptNo = revisionNo, RequestId = Guid.NewGuid(), Explanation = $"Existing attempt {revisionNo}: café <encoded text>\nSecond line.",
                    SubmittedBy = ids["Candidate"], ReceivedAt = received, RecordedAt = received.AddTicks(5) };
                submissions.Add(submission);
                db.Submissions.Add(submission);
                for (var evidenceNo = 1; evidenceNo <= 2; evidenceNo++)
                    db.SubmissionEvidence.Add(new() { SubmissionId = submission.SubmissionId, SequenceNo = evidenceNo,
                        Label = $"Evidence {evidenceNo}: résumé", Url = $"https://example.com/assignment-{assignmentNo}/attempt-{revisionNo}/{evidenceNo}" });
            }
        }
        await db.SaveChangesAsync();
        return new(submissions[1], submissions[2], administrator, ids["Mentor"]);
    }

    internal sealed record Seed(Submission First, Submission Second, string AdministratorId, string MentorId);
}
