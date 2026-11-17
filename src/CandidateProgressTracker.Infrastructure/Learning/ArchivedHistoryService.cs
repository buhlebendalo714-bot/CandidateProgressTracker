using System.Data;
using System.Text.Json;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Learning;
public sealed class ArchivedHistoryService(TrackerDbContext db):IArchivedHistoryService
{
    public async Task<IReadOnlyList<ArchiveScope>?> GetScopesAsync(string actor)
    {
        if(!await AdministratorAccess.IsCurrentAsync(db,actor))return null;
        var cycles=await db.LearningCycles.AsNoTracking().OrderByDescending(c=>c.StartsOn).ToListAsync();
        var projects=await db.Projects.AsNoTracking().OrderBy(p=>p.Title).ToListAsync();
        return cycles.Select(c=>new ArchiveScope(c.CycleId,"Cycle",c.Name,c.IsArchived,Convert.ToBase64String(c.RowVersion)))
            .Concat(projects.Select(p=>new ArchiveScope(p.ProjectId,"Project",p.Title,p.IsArchived,Convert.ToBase64String(p.RowVersion)))).ToList();
    }
    public async Task<ArchiveDetails?> GetAsync(Guid id,string kind,string actor)
    {
        var scopes=await GetScopesAsync(actor);var scope=scopes?.SingleOrDefault(s=>s.Id==id&&s.Kind==kind);if(scope is null)return null;
        var assignments=await db.Assignments.AsNoTracking().Where(a=>kind=="Cycle"?a.CycleId==id:a.ProjectId==id).ToListAsync();
        var rows=new List<ArchiveAssignment>();
        foreach(var a in assignments){var t=await AssignmentAccess.TargetAsync(db,a.AssignmentId,true);if(t is null)continue;var owner=a.TeamId is {} team?await db.ProjectTeams.Where(x=>x.TeamId==team).Select(x=>x.Name).SingleAsync():await db.Users.Where(x=>x.Id==a.CandidateId).Select(x=>x.UserName).SingleAsync();rows.Add(new(a.AssignmentId,t.Group+" / "+t.Title,owner??""));}
        var saved=await(from e in db.ScopeArchiveEvents join u in db.Users on e.ActorId equals u.Id where e.ScopeId==id&&e.ScopeType==kind orderby e.OccurredAt descending select new {e,u.UserName}).ToListAsync();
        var events=saved.Select(x=>new ArchiveAction(x.e.OccurredAt,x.UserName!,x.e.Archived?"Archived":"Reopened",x.e.Reason,JsonSerializer.Deserialize<List<ArchiveOutcome>>(x.e.OutcomeSnapshot)??[])).ToList();
        return new(scope,rows,events);
    }
    public async Task<string?> ChangeAsync(ArchiveRequest request,string actor)
    {
        if(request.Kind is not ("Cycle" or "Project")||request.RequestId==Guid.Empty||string.IsNullOrWhiteSpace(request.Reason)||request.Reason.Trim().Length>1000)return "Select a scope and give a reason of 1–1000 characters.";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Cohort gate -> scope row -> current role/ancestors -> snapshots -> write.
        var cohort=request.Kind=="Cycle"?await db.Database.SqlQuery<Guid>($"SELECT [CohortId] AS [Value] FROM LearningCycles WITH(NOLOCK) WHERE CycleId={request.ScopeId}").SingleOrDefaultAsync():await db.Database.SqlQuery<Guid>($"SELECT c.CohortId AS [Value] FROM Projects p WITH(NOLOCK) JOIN LearningCycles c WITH(NOLOCK) ON p.CycleId=c.CycleId WHERE p.ProjectId={request.ScopeId}").SingleOrDefaultAsync();
        if(cohort==Guid.Empty)return "This learning cycle or project is unavailable, or you no longer have access. Return to the archive list and try again.";
        await CohortWorkflowGate.LockAsync(db,cohort);
        if(!await AdministratorAccess.IsCurrentAsync(db,actor))return "Current Administrator access is required.";
        var old=await db.ScopeArchiveEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.ActorId==actor&&e.RequestId==request.RequestId);
        if(old is not null)return old.ScopeId==request.ScopeId&&old.ScopeType==request.Kind&&old.ExpectedVersion==request.Version&&old.Archived==request.Archive&&old.Reason==request.Reason.Trim()?null:"Request ID was already used for different content.";
        if(await db.Cohorts.AnyAsync(c=>c.CohortId==cohort&&c.IsArchived))return "The cohort is archived. Its records remain read-only.";
        if(request.Kind=="Project")
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT ProjectId FROM Projects WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId={request.ScopeId}");
            var p=await db.Projects.SingleAsync(p=>p.ProjectId==request.ScopeId);await db.Entry(p).ReloadAsync();
            if(await db.LearningCycles.AnyAsync(c=>c.CycleId==p.CycleId&&c.IsArchived))return "The cycle is archived. Reopen it first.";
            if(Convert.ToBase64String(p.RowVersion)!=request.Version||p.IsArchived==request.Archive)return "This cycle or project changed while you were working. Refresh the page and review it before trying again.";
            p.IsArchived=request.Archive;p.ArchivedAt=request.Archive?DateTimeOffset.UtcNow:null;p.ArchivedBy=request.Archive?actor:null;p.ArchiveReason=request.Archive?request.Reason.Trim():null;
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT CycleId FROM LearningCycles WITH(UPDLOCK,HOLDLOCK) WHERE CycleId={request.ScopeId}");
            var c=await db.LearningCycles.SingleAsync(c=>c.CycleId==request.ScopeId);await db.Entry(c).ReloadAsync();
            if(Convert.ToBase64String(c.RowVersion)!=request.Version||c.IsArchived==request.Archive)return "This cycle or project changed while you were working. Refresh the page and review it before trying again.";
            c.IsArchived=request.Archive;c.ArchivedAt=request.Archive?DateTimeOffset.UtcNow:null;c.ArchivedBy=request.Archive?actor:null;c.ArchiveReason=request.Archive?request.Reason.Trim():null;
        }
        var ids=await db.Assignments.Where(a=>request.Kind=="Cycle"?a.CycleId==request.ScopeId:a.ProjectId==request.ScopeId).Select(a=>a.AssignmentId).ToListAsync();
        var outcomes=new List<ArchiveOutcome>();foreach(var id in ids)
        {
            var state=await AssignmentWorkflow.LoadAsync(db,id);var target=(await AssignmentAccess.TargetAsync(db,id,true))!;var a=target.Assignment;
            var owner=a.TeamId is {} team?await db.ProjectTeams.Where(t=>t.TeamId==team).Select(t=>t.Name).SingleAsync():await db.Users.Where(u=>u.Id==a.CandidateId).Select(u=>u.UserName).SingleAsync();
            var status=state.EffectiveApproval is not null?"Approved":state.Latest is null?"Not submitted":!state.Eligible(state.Latest)?"Requirements changed":state.LatestReview?.Decision==ReviewDecision.ChangesRequested?"Changes requested":"Awaiting review";
            outcomes.Add(new(id,target.Group+" / "+target.Title,owner??"",a.OfferingId is not null,a.IsRequired,status,state.Latest?.SubmissionId,state.Latest is null?null:state.Revisions.Single(r=>r.RevisionId==state.Latest.AssignmentRevisionId).RevisionNo,state.EffectiveApproval is not null));
        }
        db.ScopeArchiveEvents.Add(new(){ScopeId=request.ScopeId,ScopeType=request.Kind,ActorId=actor,RequestId=request.RequestId,ExpectedVersion=request.Version,Archived=request.Archive,Reason=request.Reason.Trim(),OccurredAt=DateTimeOffset.UtcNow,OutcomeSnapshot=JsonSerializer.Serialize(outcomes)});
        await db.SaveChangesAsync();await tx.CommitAsync();return null;
    }
    public async Task<SubmissionHistoryPage?> HistoryAsync(Guid assignmentId,string actor,int page=1)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if(!await AdministratorAccess.IsCurrentAsync(db,actor))return null;
        var t=await AssignmentAccess.TargetAsync(db,assignmentId,true);if(t is null)return null;
        var state=await AssignmentWorkflow.LoadAsync(db,assignmentId);page=Math.Clamp(page,1,int.MaxValue/20);
        var submissions=state.Submissions.OrderByDescending(s=>s.ReceivedAt).ThenByDescending(s=>s.AttemptNo).Skip((page-1)*20).Take(21).ToList();var attempts=new List<SubmissionHistoryAttempt>();
        foreach(var s in submissions.Take(20))attempts.Add(await AssignmentWorkflow.AttemptAsync(db,state,s));
        var roster=t.Assignment.TeamId is null?null:await TeamSubmissionAccess.RosterAsync(db,assignmentId);
        var name=t.Assignment.CandidateId is null?null:await db.Users.Where(u=>u.Id==t.Assignment.CandidateId).Select(u=>u.UserName).SingleAsync();
        await tx.CommitAsync();return new(assignmentId,t.Group,t.Title,page,submissions.Count>20,attempts){CandidateName=name,Roster=roster,Eligibility="Administrator read-only history. "+state.Eligibility};
    }
}
