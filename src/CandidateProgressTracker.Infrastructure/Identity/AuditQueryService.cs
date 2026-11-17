using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Identity;
public sealed class AuditQueryService(TrackerDbContext db):IAuditQueryService
{
    private sealed class AuditProjection
    {
        public Guid Id {get;set;}
        public string? ActorId {get;set;}
        public string EntityType {get;set;}="";
        public string EntityId {get;set;}="";
        public string Action {get;set;}="";
        public string Details {get;set;}="";
        public DateTimeOffset OccurredAt {get;set;}
        public string Source {get;set;}="";
    }
    public async Task<AuditPage?> SearchAsync(AuditFilter filter,string actor)
    {
        if(!await AdministratorAccess.IsCurrentAsync(db,actor))return null;
        var page=Math.Clamp(filter.Page,1,10000);var limit=page*20+1;
        IQueryable<AuditProjection> current=db.AuditEvents.AsNoTracking().Select(e=>new AuditProjection {Id = e.AuditEventId, ActorId = e.ActorId, EntityType = e.EntityType, EntityId = e.EntityId, Action = e.Action, Details = e.Details, OccurredAt = e.OccurredAt, Source = "Audit event"});
        var queries=new List<IQueryable<AuditProjection>> {current};
        // Existing typed history remains authoritative. Read it without inventing
        // backfilled events or duplicating records captured by the new stream.
        queries.Add(db.Submissions.Where(e=>!db.AuditEvents.Any(a=>a.EntityType=="Submission"&&a.EntityId==e.SubmissionId.ToString())).Select(e=>new AuditProjection {Id = e.SubmissionId, ActorId = e.SubmittedBy, EntityType = "Submission", EntityId = e.SubmissionId.ToString(), Action = "Added", Details = "Historical receipt", OccurredAt = e.RecordedAt, Source = "Existing typed history"}));
        queries.Add(db.SubmissionReviews.Where(e=>!db.AuditEvents.Any(a=>a.EntityType=="SubmissionReview"&&a.EntityId==e.ReviewId.ToString())).Select(e=>new AuditProjection {Id = e.ReviewId, ActorId = e.ReviewerId, EntityType = "SubmissionReview", EntityId = e.ReviewId.ToString(), Action = "Added", Details = "Historical decision", OccurredAt = e.DecidedAt, Source = "Existing typed history"}));
        queries.Add(db.AssignmentReopenings.Where(e=>!db.AuditEvents.Any(a=>a.EntityType=="AssignmentReopening"&&a.EntityId==e.ReopeningId.ToString())).Select(e=>new AuditProjection {Id = e.ReopeningId, ActorId = e.AdministratorId, EntityType = "AssignmentReopening", EntityId = e.ReopeningId.ToString(), Action = "Added", Details = e.Reason, OccurredAt = e.ReopenedAt, Source = "Existing typed history"}));
        queries.Add(db.AssignmentRevisions.Where(e=>!db.AuditEvents.Any(a=>a.EntityType=="AssignmentRevision"&&a.EntityId==e.RevisionId.ToString())).Select(e=>new AuditProjection {Id = e.RevisionId, ActorId = e.ChangedBy, EntityType = "AssignmentRevision", EntityId = e.RevisionId.ToString(), Action = "Added", Details = e.Reason, OccurredAt = e.ChangedAt, Source = "Existing typed history"}));
        queries.Add(db.CohortMemberships.Where(e=>e.EndedAt!=null&&!db.AuditEvents.Any(a=>a.EntityType=="CohortMembership"&&a.EntityId==e.MembershipId.ToString())).Select(e=>new AuditProjection {Id = e.MembershipId, ActorId = e.EndedBy, EntityType = "CohortMembership", EntityId = e.MembershipId.ToString(), Action = "Ended", Details = e.EndReason??"Historical membership ending", OccurredAt = e.EndedAt!.Value, Source = "Existing typed history"}));
        var rows=new List<AuditProjection>();
        foreach(var source in queries)
        {
            var q=source;
            if(!string.IsNullOrWhiteSpace(filter.Actor))q=q.Where(e=>e.ActorId!=null&&e.ActorId.Contains(filter.Actor));
            if(!string.IsNullOrWhiteSpace(filter.Action))q=q.Where(e=>e.Action.Contains(filter.Action));
            if(!string.IsNullOrWhiteSpace(filter.Record))q=q.Where(e=>e.EntityType.Contains(filter.Record)||e.EntityId.Contains(filter.Record));
            if(filter.From is {} from)q=q.Where(e=>e.OccurredAt>=from);
            if(filter.To is {} to)q=q.Where(e=>e.OccurredAt<=to);
            rows.AddRange(await q.OrderByDescending(e=>e.OccurredAt).ThenByDescending(e=>e.Id).Take(limit).ToListAsync());
        }
        var selected=rows.OrderByDescending(e=>e.OccurredAt).ThenByDescending(e=>e.Id).Skip((page-1)*20).Take(21).ToList();
        var visible=selected.Take(20).ToList();
        var ids=visible.Select(e=>e.EntityId).ToArray();
        var changes=await db.AccountChanges.AsNoTracking().Where(e=>ids.Contains(e.AccountChangeId.ToString())).ToListAsync();
        // Read metadata only. Never load token/password hashes for presentation.
        var codes=await db.AccountCredentials.AsNoTracking().Where(e=>ids.Contains(e.CredentialId.ToString()))
            .Select(e=>new {Id=e.CredentialId.ToString(),e.UserId,e.Kind}).ToListAsync();
        var memberships=await db.CohortMemberships.AsNoTracking().Where(e=>ids.Contains(e.MembershipId.ToString()))
            .Select(e=>new {Id=e.MembershipId.ToString(),e.UserId,e.MembershipKind,e.ChangeReason,e.EndReason}).ToListAsync();
        var reopenings=await db.AssignmentReopenings.AsNoTracking().Where(e=>ids.Contains(e.ReopeningId.ToString()))
            .Select(e=>new {Id=e.ReopeningId.ToString(),e.Reason}).ToListAsync();
        var userIds=visible.Select(e=>e.ActorId).Concat(visible.Where(e=>e.EntityType=="IdentityUser").Select(e=>e.EntityId))
            .Concat(changes.Select(e=>e.UserId)).Concat(codes.Select(e=>e.UserId)).Concat(memberships.Select(e=>e.UserId)).Distinct().ToArray();
        var names=await db.Users.AsNoTracking().Where(u=>userIds.Contains(u.Id)).ToDictionaryAsync(u=>u.Id,u=>u.UserName);
        var display=visible.Select(e=>
        {
            var change=changes.SingleOrDefault(c=>e.EntityType=="AccountChange"&&c.AccountChangeId.ToString()==e.EntityId);
            var code=codes.SingleOrDefault(c=>e.EntityType=="AccountCredential"&&c.Id==e.EntityId);
            var membership=memberships.SingleOrDefault(c=>e.EntityType=="CohortMembership"&&c.Id==e.EntityId);
            var reopening=reopenings.SingleOrDefault(c=>e.EntityType=="AssignmentReopening"&&c.Id==e.EntityId);
            var userId=change?.UserId??code?.UserId??membership?.UserId??(e.EntityType=="IdentityUser"?e.EntityId:null);
            var label=change?.Action switch {
                "Recovery issued"=>"Password reset code created",
                "Activation issued"=>"Activation code created",
                "Recovery completed"=>"Password reset completed",
                "Activation completed"=>"Account activated",
                "Deactivated"=>"Account deactivated",
                _=>change?.Action };
            if(code is not null&&e.Action=="Added")label=code.Kind=="Recovery"?"Password reset code created":"Activation code created";
            if(membership is not null)label=e.Action=="Added"?$"{membership.MembershipKind} added to cohort":e.Action=="Ended"?"Cohort membership ended":"Cohort membership updated";
            if(reopening is not null)label="Assignment approval reopened";
            return new AuditRow(e.Id,e.ActorId,e.EntityType,e.EntityId,e.Action,e.Details,e.OccurredAt,e.Source) {
                ActorName=e.ActorId is not null?names.GetValueOrDefault(e.ActorId):null,
                AffectedName=userId is not null?names.GetValueOrDefault(userId):null,
                DisplayAction=label,
                Reason=change?.Reason??reopening?.Reason??(membership is not null?(e.Action=="Added"?membership.ChangeReason:membership.EndReason):null)
            };
        }).ToList();
        return new(page,selected.Count>20,display);
    }
}
