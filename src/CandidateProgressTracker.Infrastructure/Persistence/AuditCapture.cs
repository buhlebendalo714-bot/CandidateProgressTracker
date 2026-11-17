using CandidateProgressTracker.Domain.Auditing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Persistence;
internal static class AuditCapture
{
    internal static List<AuditEvent> Prepare(TrackerDbContext db)
    {
        db.ChangeTracker.DetectChanges();var result=new List<AuditEvent>();
        foreach(var entry in db.ChangeTracker.Entries().ToArray())
        {
            if(entry.Entity is AuditEvent||entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))continue;
            var type=entry.Metadata.ClrType;
            if(!(type.Namespace?.StartsWith("CandidateProgressTracker.Domain.")==true||type==typeof(IdentityUser)||type==typeof(IdentityUserRole<string>)))continue;
            var id=string.Join("/",entry.Properties.Where(p=>p.Metadata.IsPrimaryKey()).Select(p=>p.CurrentValue?.ToString()??""));
            var actor=db.AuditActorId;
            foreach(var name in new[]{"ActorId","AdministratorId","ReviewerId","SubmittedBy","FrozenBy","EndedBy","ChangedBy","ArchivedBy","CreatedBy"})
            {
                if(actor is not null)break;
                actor=entry.Properties.FirstOrDefault(p=>p.Metadata.Name==name)?.CurrentValue as string;
            }
            // Field names only; never persist password hashes, credentials, evidence
            // contents, private paths or token values in the audit stream.
            var fields=entry.Properties.Where(p=>entry.State!=EntityState.Modified||p.IsModified)
                .Select(p=>p.Metadata.Name).Where(n=>!new[]{"PasswordHash","SecurityStamp","TokenHash","ConcurrencyStamp"}.Contains(n));
            result.Add(new(){ActorId=actor,EntityType=type.Name,EntityId=id,Action=entry.State.ToString(),Details=string.Join(", ",fields),OccurredAt=DateTimeOffset.UtcNow});
        }
        return result;
    }
}
