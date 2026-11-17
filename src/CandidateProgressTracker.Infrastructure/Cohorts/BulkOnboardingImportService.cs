using System.Data;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public sealed class BulkOnboardingImportService(TrackerDbContext db, UserManager<IdentityUser> users,
    IBulkOnboardingPreviewService previews) : IBulkOnboardingImportService
{
    private static bool Done(string status) => status is "Created" or "Enrolled" or "Skipped";
    public async Task<bool> IsAvailableAsync(string actor) => await AdministratorAccess.IsCurrentAsync(db,actor) &&
        await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID(N'dbo.OnboardingBatches',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.OnboardingBatchRows',N'U') IS NOT NULL THEN 1 ELSE 0 END AS [Value]").SingleAsync() == 1;

    public async Task<IReadOnlyList<OnboardingSavedBatch>> ListAsync(Guid cohortId,string actor)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if(!await IsAvailableAsync(actor))return [];
        return await db.OnboardingBatches.AsNoTracking().Where(b=>b.CohortId==cohortId&&b.ActorId==actor)
            .OrderByDescending(b=>b.CreatedAt).Take(20).Select(b=>new OnboardingSavedBatch(b.BatchId,b.CreatedAt)).ToListAsync();
    }

    private async Task GateAsync(Guid cohort, string actor, string? target, Guid batch)
    {
        var gates = await db.Database.SqlQuery<Guid>($"SELECT DISTINCT CohortId AS [Value] FROM CohortMemberships WITH(NOLOCK) WHERE UserId={actor} OR UserId={target}").ToListAsync();
        foreach(var id in gates.Append(cohort).Distinct().Order()) await CohortWorkflowGate.LockAsync(db,id);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AccountLifecycle',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51010,'Account operation is busy; retry.',1;");
        var resource="OnboardingBatch:"+batch.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51010,'Batch is busy; retry.',1;");
    }
    private async Task<bool> EligibleAsync(Guid cohort, string actor) =>
        await AdministratorAccess.IsCurrentAsync(db,actor) && await db.Cohorts.AnyAsync(c=>c.CohortId==cohort&&!c.IsArchived);

    public async Task<OnboardingImportResult?> ConfirmAsync(OnboardingConfirmation request, string actor)
    {
        if(request.Csv.Length>IBulkOnboardingPreviewService.MaximumBytes || request.Actor!=actor || request.RequestId==Guid.Empty || !await IsAvailableAsync(actor)) return null;
        db.ChangeTracker.Clear();
        await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await GateAsync(request.CohortId,actor,null,request.RequestId);
            if(!await EligibleAsync(request.CohortId,actor)) return null;
            var existing=await db.OnboardingBatches.SingleOrDefaultAsync(b=>b.BatchId==request.RequestId);
            if(existing is not null)
            {
                if(existing.ActorId!=actor||existing.CohortId!=request.CohortId||existing.StartDate!=request.StartDate||existing.Fingerprint!=request.Fingerprint||existing.PayloadHash!=Convert.ToHexString(SHA256.HashData(request.Csv)))
                    return new(request.RequestId,false,"This request does not match the saved batch. Upload and preview again.",[]);
            }
            else
            {
                var preview=await previews.PreviewAsync(request.CohortId,request.StartDate,request.Csv,actor);
                if(preview is null) return null;
                if(preview.Issues.Count>0||preview.Rows.Count==0||preview.Rows.Any(r=>r.Action==OnboardingAction.Conflict)||OnboardingFingerprint.Of(preview)!=request.Fingerprint)
                    return new(request.RequestId,false,"The preview is invalid or eligibility changed. Upload and review a fresh preview.",[]);
                db.OnboardingBatches.Add(new(){BatchId=request.RequestId,CohortId=request.CohortId,ActorId=actor,StartDate=request.StartDate,Fingerprint=request.Fingerprint,PayloadHash=Convert.ToHexString(SHA256.HashData(request.Csv)),CreatedAt=DateTimeOffset.UtcNow});
                db.OnboardingBatchRows.AddRange(preview.Rows.Select(r=>new OnboardingBatchRow {BatchId=request.RequestId,Row=r.Row,UserName=r.UserName,Email=r.Email,Action=r.Action.ToString(),ExpectedUserId=r.MatchedUserId}));
                await db.SaveChangesAsync();
            }
            await tx.CommitAsync();
        }
        return await ResumeAsync(request.RequestId,actor);
    }

    public async Task<OnboardingImportResult?> GetAsync(Guid batchId,string actor)
    {
        await using var queryTx=db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        if(!await IsAvailableAsync(actor)) return null;
        var batch=await db.OnboardingBatches.AsNoTracking().SingleOrDefaultAsync(b=>b.BatchId==batchId&&b.ActorId==actor);
        if(batch is null)return null;
        var rows=await db.OnboardingBatchRows.AsNoTracking().Where(r=>r.BatchId==batchId).OrderBy(r=>r.Row).Select(r=>new OnboardingImportRow(r.Row,r.UserName,r.Status,r.Message,r.UserId ?? (r.Status=="Skipped" ? r.ExpectedUserId : null))).ToListAsync();
        return new(batchId,rows.All(r=>Done(r.Status)),null,rows);
    }

    public async Task<OnboardingImportResult?> ResumeAsync(Guid batchId,string actor)
    {
        if(!await IsAvailableAsync(actor))return null;
        var batch=await db.OnboardingBatches.AsNoTracking().SingleOrDefaultAsync(b=>b.BatchId==batchId&&b.ActorId==actor);
        if(batch is null)return null;
        var numbers=await db.OnboardingBatchRows.AsNoTracking().Where(r=>r.BatchId==batchId).OrderBy(r=>r.Row).Select(r=>r.Row).ToListAsync();
        foreach(var number in numbers)
        {
            db.ChangeTracker.Clear();
            var routing=await db.OnboardingBatchRows.AsNoTracking().SingleAsync(r=>r.BatchId==batchId&&r.Row==number);
            string? failure=null;
            await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                try
                {
                    await GateAsync(batch.CohortId,actor,routing.ExpectedUserId,batchId);
                    if(!await EligibleAsync(batch.CohortId,actor))
                    {
                        await tx.RollbackAsync();
                        await tx.DisposeAsync();
                        var stopped=await GetAsync(batchId,actor);
                        return stopped is null ? null : stopped with {Message="Processing stopped because cohort eligibility changed. Completed rows are retained."};
                    }
                    var row=await db.OnboardingBatchRows.SingleAsync(r=>r.BatchId==batchId&&r.Row==number);
                    if(Done(row.Status)) {await tx.CommitAsync();continue;}
                    static string Quote(string value)=>"\""+value.Replace("\"","\"\"")+"\"";
                    var csv=Encoding.UTF8.GetBytes("UserName,Email\r\n"+Quote(row.UserName)+","+Quote(row.Email)+"\r\n");
                    var current=await previews.PreviewAsync(batch.CohortId,batch.StartDate,csv,actor);
                    var proposed=current?.Rows.SingleOrDefault();
                    if(current is null||current.Issues.Count>0||proposed is null||proposed.Action.ToString()!=row.Action||proposed.MatchedUserId!=row.ExpectedUserId)
                        failure="Eligibility changed or this row conflicts. Resolve individually, then retry or use a fresh preview.";
                    else if(row.Action==nameof(OnboardingAction.Skip))
                    {row.Status="Skipped";row.UserId=proposed.MatchedUserId;row.Message="Already enrolled; membership unchanged.";}
                    else
                    {
                        var userId=row.ExpectedUserId;
                        if(row.Action==nameof(OnboardingAction.Create))
                        {
                            var user=new IdentityUser {UserName=row.UserName,Email=row.Email,LockoutEnabled=true};
                            var created=await users.CreateAsync(user);
                            if(!created.Succeeded)failure="Account creation failed validation. Review the account individually and retry.";
                            else
                            {
                                var role=await users.AddToRoleAsync(user,"Candidate");
                                if(!role.Succeeded)failure="Candidate role could not be assigned. Retry after individual correction.";
                                else
                                {
                                    userId=user.Id;
                                    db.AccountAccessStates.Add(new(){UserId=user.Id,Enabled=false,ChangedBy=actor,ChangedAt=DateTimeOffset.UtcNow,Reason="Bulk onboarding; administrator activation required"});
                                    db.AccountChanges.Add(new(){UserId=user.Id,ActorId=actor,OccurredAt=DateTimeOffset.UtcNow,Action="Pending account created",Reason="Bulk cohort onboarding"});
                                }
                            }
                        }
                        if(failure is null)
                        {
                            var membership=new CohortMembership {CohortId=batch.CohortId,UserId=userId!,MembershipKind=MembershipKind.Candidate,
                                StartsAt=new DateTimeOffset(batch.StartDate.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(2)),CreatedBy=actor,ChangeReason="Bulk onboarding batch "+batchId};
                            db.CohortMemberships.Add(membership);
                            row.UserId=userId;row.MembershipId=membership.MembershipId;
                            row.Status=row.Action==nameof(OnboardingAction.Create)?"Created":"Enrolled";
                            row.Message=row.Status=="Created"?"Pending Candidate and membership created. Activation has not been issued.":"Existing Candidate enrolled; account unchanged.";
                        }
                    }
                    if(failure is null){await db.SaveChangesAsync();await tx.CommitAsync();}
                    else await tx.RollbackAsync();
                }
                catch(Exception exception) when(exception is DbUpdateException or SqlException)
                {
                    await tx.RollbackAsync();failure="The row could not be saved. No account or membership from this row was committed; retry.";
                }
            }
            if(failure is not null)
            {
                db.ChangeTracker.Clear();
                await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
                {
                await GateAsync(batch.CohortId,actor,routing.ExpectedUserId,batchId);
                if(await EligibleAsync(batch.CohortId,actor))
                {
                    var row=await db.OnboardingBatchRows.SingleAsync(r=>r.BatchId==batchId&&r.Row==number);
                    // Another confirmed retry may have completed it after our rollback.
                    if(!Done(row.Status)){row.Status="Failed";row.Message=failure;await db.SaveChangesAsync();}
                    await tx.CommitAsync();
                }
                }
                var partial=await GetAsync(batchId,actor);
                return partial is null?null:partial with {Message="Processing stopped. Completed rows are retained; retry resumes only unfinished rows."};
            }
        }
        return await GetAsync(batchId,actor);
    }
}
