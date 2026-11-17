using System.Text.Json.Nodes;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class TeamMigrationUpgradeTests
{
    internal const string Predecessor = "20261106175348_AddIndividualSubmissionReviews";
    internal const string Target = "20261106192833_AddTeamSubmissionRosters";
    [Fact]
    public async Task Populated_upgrade_preserves_every_existing_value_and_rowversion_without_fabricating_attribution()
    {
        await using var database = await TestDatabase.CreateAsync(Predecessor);
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        await using var legacy = new PreTeamDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<TrackerDbContext>>());
        Assert.Null(legacy.Model.FindEntityType(typeof(Submission))!.FindProperty(nameof(Submission.CreditedRosterAssignmentId)));
        Assert.StartsWith("CandidateProgressTracker_Tests_", new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog);
        var migrations = db.Database.GetMigrations().ToList(); Assert.Equal(Predecessor, migrations[migrations.IndexOf(Target) - 1]);
        var seed = await ReviewMigrationUpgradeTests.SeedAsync(legacy, scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        var review = new SubmissionReview { AssignmentId = seed.First.AssignmentId, SubmissionId = seed.First.SubmissionId,
            ReviewNo = 1, Decision = ReviewDecision.Approved, ReviewerId = seed.MentorId, Feedback = "Existing exact feedback <keep>",
            DecidedAt = DateTimeOffset.UtcNow.AddMinutes(-2), RequestId = Guid.NewGuid() };
        legacy.SubmissionReviews.Add(review);
        legacy.AssignmentReopenings.Add(new() { AssignmentId = review.AssignmentId, ApprovalReviewId = review.ReviewId,
            AdministratorId = seed.AdministratorId, Reason = "Existing reopening audit", ReopenedAt = DateTimeOffset.UtcNow.AddMinutes(-1), RequestId = Guid.NewGuid() });
        var candidate = seed.First.SubmittedBy;
        var oldAssignment = await legacy.Assignments.SingleAsync(a => a.AssignmentId == seed.First.AssignmentId);
        var project = new Project { CycleId = oldAssignment.CycleId, Mode = ProjectMode.Team, Title = "Existing team project", Brief = "Preserve", CreatedBy = seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
        var team = new ProjectTeam { ProjectId = project.ProjectId, Name = "Existing team", CreatedBy = seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
        var tm = new TeamMembership { TeamId = team.TeamId, ProjectId = project.ProjectId, UserId = candidate,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), ChangedBy = seed.AdministratorId, ChangeReason = "Existing team audit" };
        legacy.AddRange(project, team, tm);
        var assignments = new List<Assignment>(); var revisions = new List<AssignmentRevision>();
        for (var i = 1; i <= 2; i++)
        {
            var milestone = new Milestone { ProjectId = project.ProjectId, SequenceNo = i, Title = $"Existing team milestone {i}", Instructions = "Saved", AcceptanceExpectations = "Saved", EvidencePolicy = "Saved", CreatedBy = seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
            var assignment = new Assignment { ProjectId = project.ProjectId, CycleId = project.CycleId, MilestoneId = milestone.MilestoneId, TeamId = team.TeamId, DueAt = DateTimeOffset.UtcNow.AddDays(2), CreatedBy = seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
            var revision = new AssignmentRevision { AssignmentId = assignment.AssignmentId, RevisionNo = 1, DueAt = assignment.DueAt, InstructionsSnapshot = "Saved", AcceptanceSnapshot = "Saved", EvidencePolicySnapshot = "Saved", ChangedBy = seed.AdministratorId, ChangedAt = DateTimeOffset.UtcNow, Reason = "Initial" };
            legacy.AddRange(milestone, assignment, revision); assignments.Add(assignment); revisions.Add(revision);
        }
        await legacy.SaveChangesAsync();
        var before = await SnapshotAsync(legacy);
        legacy.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync(Target);
        Assert.Equal(Target, (await db.Database.GetAppliedMigrationsAsync()).Last());
        var after = await SnapshotAsync(db);
        // Only the added nullable column may differ; verify every other stored value,
        // including binary rowversions, timestamps and audit actor/reason columns.
        var submissions = JsonNode.Parse(after["Submissions"])!.AsArray();
        foreach (var submission in submissions)
        { Assert.Null(submission!["CreditedRosterAssignmentId"]); submission.AsObject().Remove("CreditedRosterAssignmentId"); }
        after["Submissions"] = submissions.ToJsonString();
        before["Submissions"] = JsonNode.Parse(before["Submissions"])!.ToJsonString();
        foreach (var table in before.Keys) Assert.Equal(before[table], after[table]);
        Assert.Empty(await db.AssignmentCreditedRosters.ToListAsync()); Assert.Empty(await db.AssignmentCreditedRosterMembers.ToListAsync());
        Assert.Equal(1, await db.SubmissionReviews.CountAsync()); Assert.Equal(1, await db.AssignmentReopenings.CountAsync());

        var cm = await db.CohortMemberships.SingleAsync(m => m.UserId == candidate);
        foreach (var assignment in assignments)
        {
            db.AssignmentCreditedRosters.Add(new() { AssignmentId = assignment.AssignmentId, ProjectId = project.ProjectId,
                TeamId = team.TeamId, CohortId = cm.CohortId, TeamNameSnapshot = team.Name, FrozenBy = candidate, FrozenAt = DateTimeOffset.UtcNow });
            db.AssignmentCreditedRosterMembers.Add(new() { AssignmentId = assignment.AssignmentId, TeamId = team.TeamId,
                ProjectId = project.ProjectId, CohortId = cm.CohortId, UserId = candidate, UserNameSnapshot = "Snapshot",
                CohortMembershipId = cm.MembershipId, TeamMembershipId = tm.TeamMembershipId });
        }
        await db.SaveChangesAsync();
        var attempt = new Submission { AssignmentId = assignments[0].AssignmentId, AssignmentRevisionId = revisions[0].RevisionId,
            CreditedRosterAssignmentId = assignments[1].AssignmentId, SubmittedBy = candidate, RequestId = Guid.NewGuid(),
            AttemptNo = 1, Explanation = "Cross-assignment reference", ReceivedAt = DateTimeOffset.UtcNow, RecordedAt = DateTimeOffset.UtcNow };
        db.Submissions.Add(attempt);
        var cross = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(cross.InnerException).Number);
        db.Entry(attempt).State = EntityState.Detached;
        attempt.CreditedRosterAssignmentId = attempt.AssignmentId;
        db.Submissions.Add(attempt); await db.SaveChangesAsync();
        var immutable = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE AssignmentCreditedRosterMembers SET UserNameSnapshot='rewrite' WHERE AssignmentId={attempt.AssignmentId}"));
        Assert.Equal(51022, immutable.Number);
        var scopeError = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Assignments SET TeamId=NULL, CandidateId={candidate} WHERE AssignmentId={attempt.AssignmentId}"));
        Assert.Equal(51024, scopeError.Number);
        var headerImmutable = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE AssignmentCreditedRosters SET TeamNameSnapshot='rewrite' WHERE AssignmentId={attempt.AssignmentId}"));
        Assert.Equal(51020, headerImmutable.Number);
    }

    private static async Task<Dictionary<string, string>> SnapshotAsync(TrackerDbContext db)
    {
        var snapshot = await ReviewMigrationUpgradeTests.SnapshotAsync(db);
        foreach (var (table, key) in new[] { ("SubmissionReviews", "ReviewId"), ("AssignmentReopenings", "ReopeningId"), ("ProjectTeams", "TeamId"), ("TeamMemberships", "TeamMembershipId") })
        {
#pragma warning disable EF1002
            snapshot[table] = await db.Database.SqlQueryRaw<string>($"SELECT (SELECT * FROM [{table}] ORDER BY [{key}] FOR JSON PATH, INCLUDE_NULL_VALUES) AS [Value]").SingleAsync();
#pragma warning restore EF1002
        }
        return snapshot;
    }
}
