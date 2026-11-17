using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class LifecycleMigrationUpgradeTests
{
    internal const string Predecessor="20261107065418_AddTheoryCoursesAndPrivateEvidence";
    internal const string Target="20261107083104_AddAccountLifecycleAuditAndArchives";
    [Fact]
    public async Task Populated_upgrade_preserves_all_existing_rows_audit_fields_and_rowversions_without_fabricated_account_or_audit_state()
    {
        await using var database=await TestDatabase.CreateAsync(Predecessor);await using var scope=database.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed=await ReviewMigrationUpgradeTests.SeedAsync(db,scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        var assignment=await db.Assignments.SingleAsync(a=>a.AssignmentId==seed.First.AssignmentId);
        var review=new SubmissionReview{AssignmentId=assignment.AssignmentId,SubmissionId=seed.First.SubmissionId,ReviewNo=1,Decision=ReviewDecision.Approved,Feedback="Keep feedback",ReviewerId=seed.MentorId,DecidedAt=DateTimeOffset.UtcNow,RequestId=Guid.NewGuid()};
        var course=new Course{Title="Keep course",Platform="Keep provider",Url="https://example.com/course",CreatedBy=seed.AdministratorId,CreatedAt=DateTimeOffset.UtcNow};
        db.AddRange(review,course);db.AssignmentReopenings.Add(new(){AssignmentId=assignment.AssignmentId,ApprovalReviewId=review.ReviewId,AdministratorId=seed.AdministratorId,Reason="Preserve reopening",RequestId=Guid.NewGuid(),ReopenedAt=DateTimeOffset.UtcNow});
        db.CourseOfferings.Add(new(){CourseId=course.CourseId,CycleId=assignment.CycleId,TitleSnapshot=course.Title,PlatformSnapshot=course.Platform,UrlSnapshot=course.Url,CreatedBy=seed.AdministratorId,CreatedAt=course.CreatedAt});
        var job=new UploadAttempt{AssignmentId=assignment.AssignmentId,RevisionId=seed.First.AssignmentRevisionId,ActorId=seed.First.SubmittedBy,RequestId=Guid.NewGuid(),Explanation="Preserve payload",PayloadHash=new string('A',64),ExpectedVersion=assignment.RowVersion,ReceivedAt=seed.First.ReceivedAt,NextTryAt=DateTimeOffset.UtcNow,Status="Committed",SubmissionId=seed.First.SubmissionId,FinishedAt=DateTimeOffset.UtcNow};
        db.Add(job);db.EvidenceFiles.Add(new(){UploadAttemptId=job.UploadAttemptId,SequenceNo=1,OriginalName="certificate.pdf",ObjectKey=Guid.NewGuid().ToString("N"),MediaType="application/pdf",SizeBytes=123,Sha256=new string('B',64),ScanResult="Clean",ScannedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();
        var before=await SnapshotAsync(db);var migrations=db.Database.GetMigrations().ToList();Assert.Equal(Predecessor,migrations[migrations.IndexOf(Target)-1]);
        await db.GetService<IMigrator>().MigrateAsync(Target);var after=await SnapshotAsync(db);
        foreach(var table in before.Keys)Assert.Equal(before[table],after[table]);
        Assert.Empty(await db.AccountAccessStates.ToListAsync());Assert.Empty(await db.AccountCredentials.ToListAsync());Assert.Empty(await db.AccountChanges.ToListAsync());Assert.Empty(await db.AuditEvents.ToListAsync());Assert.Empty(await db.ScopeArchiveEvents.ToListAsync());
        Assert.True(await AccountAvailability.IsActiveAsync(db,seed.First.SubmittedBy));
        var query=new AuditQueryService(db);var old=await query.SearchAsync(new(Record:seed.First.SubmissionId.ToString()),seed.AdministratorId);Assert.Single(old!.Rows);Assert.Equal("Existing typed history",old.Rows[0].Source);
    }
    private static async Task<Dictionary<string,string>> SnapshotAsync(TrackerDbContext db)
    {
        var result=await ReviewMigrationUpgradeTests.SnapshotAsync(db);
        foreach(var(table,key) in new[]{("SubmissionReviews","ReviewId"),("AssignmentReopenings","ReopeningId"),("Courses","CourseId"),("CourseOfferings","OfferingId"),("UploadAttempts","UploadAttemptId"),("EvidenceFiles","EvidenceFileId"),("ProjectTeams","TeamId"),("TeamMemberships","TeamMembershipId"),("AssignmentCreditedRosters","AssignmentId"),("AssignmentCreditedRosterMembers","AssignmentId,UserId")})
        {
#pragma warning disable EF1002
            result[table]=await db.Database.SqlQueryRaw<string>($"SELECT (SELECT * FROM [{table}] ORDER BY {key} FOR JSON PATH, INCLUDE_NULL_VALUES) AS [Value]").SingleAsync();
#pragma warning restore EF1002
        }
        return result;
    }
}
