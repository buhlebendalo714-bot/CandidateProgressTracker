using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Learning;
public sealed class LearningOverviewService(TrackerDbContext db,IPrivateEvidenceStorage? storage=null) : ILearningOverviewService
{
    private static readonly string[] Statuses=["All","Awaiting review","Changes requested","Approved","Requirements changed","Not submitted"];
    private async Task<List<CycleChoice>?> CyclesAsync(string actor,MembershipKind kind)
    {
        if(!await AssignmentAccess.RoleAsync(db,actor,kind==MembershipKind.Candidate ? "CANDIDATE" : "MENTOR")) return null;
        var now=DateTimeOffset.UtcNow;
        return await (from c in db.LearningCycles.AsNoTracking() join h in db.Cohorts on c.CohortId equals h.CohortId
            where !c.IsArchived && !h.IsArchived && db.CohortMemberships.Any(m=>m.UserId==actor && m.CohortId==h.CohortId && m.MembershipKind==kind && m.StartsAt<=now && (m.EndsAt==null || m.EndsAt>now))
            orderby c.StartsOn descending,c.Name select new CycleChoice(c.CycleId,c.Name,h.CohortId,h.Name)).ToListAsync();
    }
    private async Task<ProgressAssignment> RowAsync(AssignmentAccess.Target t,bool counts,bool canSubmit)
    {
        var state=await AssignmentWorkflow.LoadAsync(db,t.Assignment.AssignmentId);var latest=state.Latest;
        var status=state.EffectiveApproval is not null ? "Approved" : latest is null ? "Not submitted"
            : !state.Eligible(latest) ? "Requirements changed" : state.LatestReview?.Decision==ReviewDecision.ChangesRequested ? "Changes requested" : "Awaiting review";
        var available=latest is null || storage is null ? latest is null || !await db.UploadAttempts.AnyAsync(a=>a.SubmissionId==latest.SubmissionId)
            : await FileEvidenceService.ReviewableAsync(db,storage,latest.SubmissionId);
        var owner=t.Assignment.TeamId is not null ? await db.ProjectTeams.Where(p=>p.TeamId==t.Assignment.TeamId).Select(p=>p.Name).SingleAsync()
            : await db.Users.Where(u=>u.Id==t.Assignment.CandidateId).Select(u=>u.UserName).SingleAsync();
        return new(t.Assignment.AssignmentId,t.Title,t.Group,t.Assignment.OfferingId is not null,t.Assignment.TeamId is not null,t.Assignment.IsRequired,counts,status,
            status is "Not submitted" or "Changes requested" or "Requirements changed",state.CanReview && available,t.Assignment.DueAt,latest?.ReceivedAt,owner??"(No name)",canSubmit && status!="Approved");
    }
    private static ProgressTotals Totals(IEnumerable<ProgressAssignment> source,DateTimeOffset now)
    {
        var rows=source.Where(r=>r.CountsForProgress).ToList();var required=rows.Where(r=>r.IsRequired).ToList();
        return new(required.Count(r=>r.Status=="Approved"),required.Count,rows.Count(r=>r.Status=="Awaiting review"),rows.Count(r=>r.Status=="Changes requested"),rows.Count(r=>r.NeedsCandidateAction && r.DueAt<now));
    }
    private async Task<List<ProgressAssignment>> CandidateRowsAsync(string candidate,Guid cycle)
    {
        var ids=await db.Assignments.AsNoTracking().Where(a=>a.CycleId==cycle).Select(a=>a.AssignmentId).ToListAsync();var rows=new List<ProgressAssignment>();
        foreach(var id in ids)
        {
            var target=await AssignmentAccess.TargetAsync(db,id);
            if(target is null || !await AssignmentAccess.CandidateAsync(db,target,candidate,false)) continue;
            var counts=target.Assignment.TeamId is null || !await db.AssignmentCreditedRosters.AnyAsync(r=>r.AssignmentId==id)
                || await db.AssignmentCreditedRosterMembers.AnyAsync(m=>m.AssignmentId==id && m.UserId==candidate);
            rows.Add(await RowAsync(target,counts,counts));
        }
        return rows.OrderBy(r=>r.DueAt).ThenBy(r=>r.AssignmentId).ToList();
    }
    public async Task<CandidateOverview?> GetCandidateAsync(string actor,Guid? cycleId=null)
    {
        var cycles=await CyclesAsync(actor,MembershipKind.Candidate);if(cycles is null) return null;
        if(cycleId is not null && !cycles.Any(c=>c.CycleId==cycleId)) return null;
        cycleId??=cycles.FirstOrDefault()?.CycleId;
        var rows=cycleId is null ? [] : await CandidateRowsAsync(actor,cycleId.Value);var now=DateTimeOffset.UtcNow;
        // Only assignments already authorised above may contribute feedback. This
        // includes shared team feedback for current joiners, never credited leavers.
        var ids=rows.Select(r=>r.AssignmentId).ToList();
        var teamIds=rows.Where(r=>r.IsTeam).Select(r=>r.AssignmentId).ToList();
        var feedback=await (from r in db.SubmissionReviews.AsNoTracking()
            join s in db.Submissions on r.SubmissionId equals s.SubmissionId
            join u in db.Users on r.ReviewerId equals u.Id
            where ids.Contains(r.AssignmentId) && (s.SubmittedBy==actor || teamIds.Contains(r.AssignmentId)) && r.Feedback!=null && r.Feedback.Trim()!=""
            orderby r.DecidedAt descending,r.ReviewId descending
            select new {r.AssignmentId,s.AttemptNo,r.Decision,r.Feedback,u.UserName,r.DecidedAt}).Take(3).ToListAsync();
        return new(cycleId,cycles,Totals(rows.Where(r=>r.IsCourse),now),Totals(rows.Where(r=>!r.IsCourse),now),rows)
        { RecentFeedback=feedback.Select(f=>new CandidateFeedback(f.AssignmentId,rows.Single(a=>a.AssignmentId==f.AssignmentId).Title,
            rows.Single(a=>a.AssignmentId==f.AssignmentId).IsTeam,f.AttemptNo,f.Decision==ReviewDecision.Approved?"Approved":"Changes requested",f.Feedback!,f.UserName??"Mentor",f.DecidedAt)).ToList() };
    }
    public async Task<MentorOverview?> GetMentorAsync(string actor,Guid? cycleId=null)
    {
        var cycles=await CyclesAsync(actor,MembershipKind.Mentor);if(cycles is null) return null;
        if(cycleId is not null && !cycles.Any(c=>c.CycleId==cycleId)) return null;
        cycleId??=cycles.FirstOrDefault()?.CycleId;var rows=new List<CandidateProgressRow>();
        if(cycleId is not null)
        {
            var cohort=cycles.Single(c=>c.CycleId==cycleId).CohortId;var now=DateTimeOffset.UtcNow;
            var candidates=await (from m in db.CohortMemberships join u in db.Users on m.UserId equals u.Id
                where m.CohortId==cohort && m.MembershipKind==MembershipKind.Candidate && m.StartsAt<=now && (m.EndsAt==null || m.EndsAt>now)
                select new {u.Id,u.UserName}).Distinct().OrderBy(u=>u.UserName).ToListAsync();
            foreach(var c in candidates)
            {if(!await AssignmentAccess.RoleAsync(db,c.Id,"CANDIDATE")) continue;var work=await CandidateRowsAsync(c.Id,cycleId.Value);rows.Add(new(c.Id,c.UserName??"(No username)",Totals(work.Where(r=>r.IsCourse),now),Totals(work.Where(r=>!r.IsCourse),now)));}
        }
        return new(cycleId,cycles,rows);
    }
    public async Task<ReviewQueuePage?> GetQueueAsync(string actor,ReviewQueueFilter filter)
    {
        if(!Statuses.Contains(filter.Status,StringComparer.Ordinal) || filter.Page is <1 or >100000) return null;
        var cycles=await CyclesAsync(actor,MembershipKind.Mentor);if(cycles is null) return null;
        if(filter.CohortId is not null && !cycles.Any(c=>c.CohortId==filter.CohortId) || filter.CycleId is not null && !cycles.Any(c=>c.CycleId==filter.CycleId)) return null;
        var cycleIds=cycles.Where(c=>(filter.CohortId is null || c.CohortId==filter.CohortId) && (filter.CycleId is null || c.CycleId==filter.CycleId)).Select(c=>c.CycleId).ToList();
        var ids=await db.Assignments.AsNoTracking().Where(a=>cycleIds.Contains(a.CycleId) && (filter.ProjectId==null || a.ProjectId==filter.ProjectId) && (filter.OfferingId==null || a.OfferingId==filter.OfferingId)
            && db.Submissions.Any(s=>s.AssignmentId==a.AssignmentId)).Select(a=>a.AssignmentId).ToListAsync();
        var rows=new List<ProgressAssignment>();
        foreach(var id in ids)
        {
            var t=await AssignmentAccess.TargetAsync(db,id);if(t is null || !await AssignmentAccess.StaffAsync(db,t,actor,"MENTOR")) continue;
            var row=await RowAsync(t,true,false);var latest=await db.Submissions.AsNoTracking().Where(s=>s.AssignmentId==id).OrderByDescending(s=>s.AttemptNo).FirstAsync();
            row=row with {CanReview=row.CanReview && !await AssignmentWorkflow.SelfReviewAsync(db,latest,actor)};
            if(filter.Status!="All" && row.Status!=filter.Status) continue;
            rows.Add(row);
        }
        var page=rows.OrderBy(r=>r.ReceivedAt).ThenBy(r=>r.AssignmentId).Skip((filter.Page-1)*20).Take(21).ToList();
        return new(filter,cycles,page.Take(20).ToList(),page.Count>20)
        {
            Projects=await db.Projects.AsNoTracking().Where(p=>cycleIds.Contains(p.CycleId) && !p.IsArchived).OrderBy(p=>p.Title).Select(p=>new QueueTargetChoice(p.ProjectId,p.Title)).ToListAsync(),
            Offerings=await db.CourseOfferings.AsNoTracking().Where(o=>cycleIds.Contains(o.CycleId)).OrderBy(o=>o.TitleSnapshot).Select(o=>new QueueTargetChoice(o.OfferingId,o.TitleSnapshot)).ToListAsync()
        };
    }
}
