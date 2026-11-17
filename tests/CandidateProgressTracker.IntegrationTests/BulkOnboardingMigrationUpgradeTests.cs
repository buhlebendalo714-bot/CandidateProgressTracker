using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class BulkOnboardingMigrationUpgradeTests
{
    private const string Predecessor="20261107083104_AddAccountLifecycleAuditAndArchives";
    private const string Target="20261116205953_AddBulkOnboardingBatches";
    [Fact]
    public async Task Populated_upgrade_preserves_every_original_table_and_blocks_populated_downgrade()
    {
        await using var database=await TestDatabase.CreateAsync(Predecessor);
        await using var scope=database.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var seed=await ReviewMigrationUpgradeTests.SeedAsync(db,scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        db.AccountAccessStates.Add(new(){UserId=seed.First.SubmittedBy,Enabled=false,ChangedBy=seed.AdministratorId,ChangedAt=DateTimeOffset.UtcNow,Reason="Preserve state"});
        db.AccountChanges.Add(new(){UserId=seed.First.SubmittedBy,ActorId=seed.AdministratorId,Action="Upgrade fixture",Reason="Preserve account history",OccurredAt=DateTimeOffset.UtcNow});
        db.AccountCredentials.Add(new(){UserId=seed.First.SubmittedBy,Kind="Activation",TokenHash=new string('A',64),IssuedBy=seed.AdministratorId,IssuedAt=DateTimeOffset.UtcNow,ExpiresAt=DateTimeOffset.UtcNow.AddHours(1)});
        db.AuditEvents.Add(new(){ActorId=seed.AdministratorId,EntityType="UpgradeFixture",EntityId=seed.First.SubmittedBy,Action="Preserve",Details="No credentials",OccurredAt=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync();
        var before=await Snapshot(db);Assert.True(before.Count>=31);
        var all=db.Database.GetMigrations().ToList();Assert.Equal(Predecessor,all[all.IndexOf(Target)-1]);
        await db.GetService<IMigrator>().MigrateAsync(Target);
        var after=await Snapshot(db);foreach(var table in before.Keys)Assert.Equal(before[table],after[table]);
        Assert.Empty(await db.OnboardingBatches.ToListAsync());Assert.Empty(await db.OnboardingBatchRows.ToListAsync());
        var cycleId=await db.Assignments.Where(a=>a.AssignmentId==seed.First.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        var cohortId=await db.LearningCycles.Where(c=>c.CycleId==cycleId).Select(c=>c.CohortId).SingleAsync();
        db.OnboardingBatches.Add(new(){BatchId=Guid.NewGuid(),ActorId=seed.AdministratorId,CohortId=cohortId,StartDate=DateOnly.FromDateTime(DateTime.UtcNow),Fingerprint=new string('A',64),PayloadHash=new string('B',64),CreatedAt=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>db.GetService<IMigrator>().MigrateAsync(Predecessor));
        Assert.Single(await db.OnboardingBatches.ToListAsync());Assert.Contains(Target,await db.Database.GetAppliedMigrationsAsync());
    }
    private sealed class UpgradeKey
    {
        public string TableName { get; set; }="";
        public string? ColumnName { get; set; }
    }
    private static async Task<Dictionary<string,string>> Snapshot(TrackerDbContext db)
    {
        // Read all original table keys once; keep every table/column/row comparison.
        var metadata=await db.Database.SqlQuery<UpgradeKey>($"SELECT t.name AS TableName,c.name AS ColumnName FROM sys.tables t LEFT JOIN sys.indexes i ON i.object_id=t.object_id AND i.is_primary_key=1 LEFT JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id LEFT JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE t.name NOT IN (N'__EFMigrationsHistory',N'OnboardingBatches',N'OnboardingBatchRows') ORDER BY t.name,ic.key_ordinal").ToListAsync();
        var result=new Dictionary<string,string>();
        foreach(var table in metadata.GroupBy(k=>k.TableName))
        {
            Assert.All(table,k=>Assert.NotNull(k.ColumnName));
            static string Quote(string value)=>"["+value.Replace("]","]]")+"]";
#pragma warning disable EF1002
            result[table.Key]=await db.Database.SqlQueryRaw<string>($"SELECT (SELECT * FROM {Quote(table.Key)} ORDER BY {string.Join(',',table.Select(k=>Quote(k.ColumnName!)))} FOR JSON PATH, INCLUDE_NULL_VALUES) AS [Value]").SingleAsync();
#pragma warning restore EF1002
        }
        return result;
    }
}
