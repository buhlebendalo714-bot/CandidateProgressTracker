using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Learning;
public sealed class CourseService(TrackerDbContext db) : ICourseService
{
    private async Task<bool> AdminAsync(Guid cycle,string actor) => await AdministratorAccess.IsCurrentAsync(db,actor)
        && await (from c in db.LearningCycles join h in db.Cohorts on c.CohortId equals h.CohortId where c.CycleId==cycle && !c.IsArchived && !h.IsArchived select c.CycleId).AnyAsync();
    public async Task<string?> CreateAsync(string title,string platform,string url,string actor)
    {
        title=title.Trim(); platform=platform.Trim(); url=url.Trim();
        if(title.Length is <1 or >200 || platform.Length is <1 or >120 || url.Length>2048 || !new SubmissionHistoryEvidence("course",url).IsClickable) return "Enter title (200), platform (120), and a valid HTTPS course link.";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if(!await AdministratorAccess.IsCurrentAsync(db,actor)) return "Current Administrator access is required.";
        db.Courses.Add(new() {Title=title,Platform=platform,Url=url,CreatedAt=DateTimeOffset.UtcNow,CreatedBy=actor});
        await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
    }
    public async Task<string?> OfferAsync(Guid courseId,Guid cycleId,string actor)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var cohort=await db.LearningCycles.Where(c=>c.CycleId==cycleId).Select(c=>c.CohortId).SingleOrDefaultAsync(); await CohortWorkflowGate.LockAsync(db,cohort);
        if(!await AdminAsync(cycleId,actor)) return "This cycle is not available.";
        var c=await db.Courses.SingleOrDefaultAsync(c=>c.CourseId==courseId);
        if(c is null || await db.CourseOfferings.AnyAsync(o=>o.CourseId==courseId && o.CycleId==cycleId)) return "Choose a catalogue course that has not already been added to this cycle.";
        db.CourseOfferings.Add(new() {CourseId=courseId,CycleId=cycleId,TitleSnapshot=c.Title,PlatformSnapshot=c.Platform,UrlSnapshot=c.Url,CreatedBy=actor,CreatedAt=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
    }
    public async Task<string?> AssignAsync(Guid offeringId,string candidate,DateTimeOffset due,string instructions,string acceptance,string evidence,bool required,string actor)
    {
        instructions=instructions.Trim(); acceptance=acceptance.Trim(); evidence=evidence.Trim(); due=due.ToUniversalTime();
        if(due<=DateTimeOffset.UtcNow || instructions.Length is <1 or >10000 || acceptance.Length is <1 or >10000 || evidence.Length is <1 or >2000) return "Enter a future deadline and complete all requirement fields within their stated limits.";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var o=await db.CourseOfferings.AsNoTracking().SingleOrDefaultAsync(o=>o.OfferingId==offeringId); if(o is null) return "Offering not found.";
        var cohort=await db.LearningCycles.Where(c=>c.CycleId==o.CycleId).Select(c=>c.CohortId).SingleAsync(); await CohortWorkflowGate.LockAsync(db,cohort);
        if(!await AdminAsync(o.CycleId,actor) || !await AssignmentAccess.RoleAsync(db,candidate,"CANDIDATE") || !await AssignmentAccess.MembershipAsync(db,cohort,candidate,MembershipKind.Candidate,DateTimeOffset.UtcNow)) return "Current Administrator and eligible candidate membership are required.";
        if(await db.Assignments.AnyAsync(a=>a.OfferingId==offeringId && a.CandidateId==candidate)) return "This course is already assigned to the candidate.";
        var a=new Assignment {CycleId=o.CycleId,OfferingId=offeringId,CandidateId=candidate,DueAt=due,IsRequired=required,CreatedAt=DateTimeOffset.UtcNow,CreatedBy=actor};
        db.Assignments.Add(a); db.AssignmentRevisions.Add(new() {AssignmentId=a.AssignmentId,RevisionNo=1,DueAt=due,InstructionsSnapshot=instructions,AcceptanceSnapshot=acceptance,EvidencePolicySnapshot=evidence,ChangedAt=DateTimeOffset.UtcNow,ChangedBy=actor,Reason="Initial course assignment"});
        await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
    }
    public async Task<CourseSetup?> GetSetupAsync(Guid cycleId,string actor)
    {
        if(!await AdminAsync(cycleId,actor)) return null;
        var cohort=await db.LearningCycles.Where(c=>c.CycleId==cycleId).Select(c=>c.CohortId).SingleAsync();
        var candidates=await (from m in db.CohortMemberships join u in db.Users on m.UserId equals u.Id where m.CohortId==cohort && m.MembershipKind==MembershipKind.Candidate && m.StartsAt<=DateTimeOffset.UtcNow && (m.EndsAt==null || m.EndsAt>DateTimeOffset.UtcNow) select new CourseCandidate(u.Id,u.UserName??"(No username)")).Distinct().ToListAsync();
        return new(await db.Courses.OrderBy(c=>c.Title).Select(c=>new CourseItem(c.CourseId,c.Title,c.Platform,c.Url)).ToListAsync(),
            await db.CourseOfferings.Where(o=>o.CycleId==cycleId).OrderBy(o=>o.TitleSnapshot).Select(o=>new CourseItem(o.OfferingId,o.TitleSnapshot,o.PlatformSnapshot,o.UrlSnapshot)).ToListAsync(),
            await (from a in db.Assignments join o in db.CourseOfferings on a.OfferingId equals o.OfferingId join u in db.Users on a.CandidateId equals u.Id where a.CycleId==cycleId select new CourseAssignmentItem(a.AssignmentId,o.TitleSnapshot,o.PlatformSnapshot,o.UrlSnapshot,u.UserName??"",a.DueAt)).ToListAsync(),candidates);
    }
    public async Task<IReadOnlyList<CourseAssignmentItem>> GetMineAsync(string actor,bool mentor=false)
    {
        var ids=await db.Assignments.Where(a=>a.OfferingId!=null).Select(a=>a.AssignmentId).ToListAsync(); var result=new List<CourseAssignmentItem>();
        foreach(var id in ids)
        {
            var t=await AssignmentAccess.TargetAsync(db,id); if(t is null) continue;
            if(mentor ? !await AssignmentAccess.StaffAsync(db,t,actor,"MENTOR") : !await AssignmentAccess.CandidateAsync(db,t,actor,false)) continue;
            var o=await db.CourseOfferings.SingleAsync(o=>o.OfferingId==t.Assignment.OfferingId);
            var name=await db.Users.Where(u=>u.Id==t.Assignment.CandidateId).Select(u=>u.UserName).SingleAsync();
            result.Add(new(id,o.TitleSnapshot,o.PlatformSnapshot,o.UrlSnapshot,name??"",t.Assignment.DueAt));
        }
        return result.OrderBy(x=>x.DueAt).ToList();
    }
}
