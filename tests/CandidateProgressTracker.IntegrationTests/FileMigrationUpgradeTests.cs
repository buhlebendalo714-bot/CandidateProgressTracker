using System.Text.Json.Nodes;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class FileMigrationUpgradeTests
{
    internal const string Predecessor="20261106192833_AddTeamSubmissionRosters";
    internal const string Target="20261107065418_AddTheoryCoursesAndPrivateEvidence";
    [Fact]
    public async Task Populated_upgrade_preserves_individual_team_evidence_reviews_reopenings_audit_and_rowversions()
    {
        await using var database=await TestDatabase.CreateAsync(Predecessor);await using var scope=database.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();await using var old=new PreFilesDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<TrackerDbContext>>());
        var migrations=db.Database.GetMigrations().ToList();Assert.Equal(Predecessor,migrations[migrations.IndexOf(Target)-1]);
        var seed=await ReviewMigrationUpgradeTests.SeedAsync(old,scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        var review=new SubmissionReview {AssignmentId=seed.First.AssignmentId,SubmissionId=seed.First.SubmissionId,ReviewNo=1,Decision=ReviewDecision.Approved,Feedback="Keep feedback",ReviewerId=seed.MentorId,DecidedAt=DateTimeOffset.UtcNow,RequestId=Guid.NewGuid()};
        old.SubmissionReviews.Add(review);old.AssignmentReopenings.Add(new() {AssignmentId=review.AssignmentId,ApprovalReviewId=review.ReviewId,AdministratorId=seed.AdministratorId,Reason="Keep reopening audit",ReopenedAt=DateTimeOffset.UtcNow,RequestId=Guid.NewGuid()});
        var original=await old.Assignments.SingleAsync(a=>a.AssignmentId==seed.First.AssignmentId);
        var project=new Project {CycleId=original.CycleId,Mode=ProjectMode.Team,Title="Existing team project",Brief="Keep",CreatedBy=seed.AdministratorId,CreatedAt=DateTimeOffset.UtcNow};
        var team=new ProjectTeam {ProjectId=project.ProjectId,Name="Keep team",CreatedBy=seed.AdministratorId,CreatedAt=DateTimeOffset.UtcNow};
        var tm=new TeamMembership {TeamId=team.TeamId,ProjectId=project.ProjectId,UserId=seed.First.SubmittedBy,StartsAt=DateTimeOffset.UtcNow.AddDays(-1),ChangedBy=seed.AdministratorId,ChangeReason="Keep source"};
        var milestone=new Milestone {ProjectId=project.ProjectId,SequenceNo=1,Title="Keep milestone",Instructions="Keep",AcceptanceExpectations="Keep",EvidencePolicy="Keep",CreatedBy=seed.AdministratorId,CreatedAt=DateTimeOffset.UtcNow};
        var assignment=new Assignment {ProjectId=project.ProjectId,CycleId=project.CycleId,MilestoneId=milestone.MilestoneId,TeamId=team.TeamId,DueAt=DateTimeOffset.UtcNow.AddDays(2),CreatedBy=seed.AdministratorId,CreatedAt=DateTimeOffset.UtcNow};
        var revision=new AssignmentRevision {AssignmentId=assignment.AssignmentId,RevisionNo=1,DueAt=assignment.DueAt,InstructionsSnapshot="Keep",AcceptanceSnapshot="Keep",EvidencePolicySnapshot="Keep",ChangedAt=DateTimeOffset.UtcNow,ChangedBy=seed.AdministratorId,Reason="Keep revision audit"};
        old.AddRange(project,team,tm,milestone,assignment,revision);await old.SaveChangesAsync();
        var cm=await old.CohortMemberships.SingleAsync(m=>m.UserId==tm.UserId);
        old.AssignmentCreditedRosters.Add(new() {AssignmentId=assignment.AssignmentId,TeamId=team.TeamId,ProjectId=project.ProjectId,CohortId=cm.CohortId,FrozenAt=DateTimeOffset.UtcNow,FrozenBy=tm.UserId,TeamNameSnapshot=team.Name});
        old.AssignmentCreditedRosterMembers.Add(new() {AssignmentId=assignment.AssignmentId,TeamId=team.TeamId,ProjectId=project.ProjectId,CohortId=cm.CohortId,UserId=tm.UserId,UserNameSnapshot="Keep frozen identity",TeamMembershipId=tm.TeamMembershipId,CohortMembershipId=cm.MembershipId});await old.SaveChangesAsync();
        var submission=new Submission {AssignmentId=assignment.AssignmentId,CreditedRosterAssignmentId=assignment.AssignmentId,AssignmentRevisionId=revision.RevisionId,AttemptNo=1,RequestId=Guid.NewGuid(),Explanation="Keep shared evidence",SubmittedBy=tm.UserId,ReceivedAt=DateTimeOffset.UtcNow,RecordedAt=DateTimeOffset.UtcNow};
        old.Submissions.Add(submission);old.SubmissionEvidence.Add(new() {SubmissionId=submission.SubmissionId,SequenceNo=1,Label="Keep link",Url="https://example.com/team"});await old.SaveChangesAsync();
        var before=await SnapshotAsync(old);await db.GetService<IMigrator>().MigrateAsync(Target);var after=await SnapshotAsync(db);
        var rows=JsonNode.Parse(after["Assignments"])!.AsArray();foreach(var row in rows) {Assert.Null(row!["OfferingId"]);row.AsObject().Remove("OfferingId");}
        after["Assignments"]=rows.ToJsonString();before["Assignments"]=JsonNode.Parse(before["Assignments"])!.ToJsonString();
        foreach(var table in before.Keys) Assert.Equal(before[table],after[table]);
        Assert.Empty(await db.Courses.ToListAsync());Assert.Empty(await db.UploadAttempts.ToListAsync());Assert.Empty(await db.EvidenceFiles.ToListAsync());
        Assert.Equal(Predecessor,(await db.Database.GetAppliedMigrationsAsync()).Reverse().Skip(1).First());
        Assert.Single(await db.AssignmentCreditedRosterMembers.Where(m=>m.AssignmentId==assignment.AssignmentId).ToListAsync());
    }
    private static async Task<Dictionary<string,string>> SnapshotAsync(TrackerDbContext db)
    {
        var result=await ReviewMigrationUpgradeTests.SnapshotAsync(db);
        foreach(var (table,key) in new[] {("SubmissionReviews","ReviewId"),("AssignmentReopenings","ReopeningId"),("ProjectTeams","TeamId"),("TeamMemberships","TeamMembershipId"),("AssignmentCreditedRosters","AssignmentId"),("AssignmentCreditedRosterMembers","AssignmentId,UserId")})
        {
#pragma warning disable EF1002
            result[table]=await db.Database.SqlQueryRaw<string>($"SELECT (SELECT * FROM [{table}] ORDER BY {key} FOR JSON PATH, INCLUDE_NULL_VALUES) AS [Value]").SingleAsync();
#pragma warning restore EF1002
        }
        return result;
    }
}
