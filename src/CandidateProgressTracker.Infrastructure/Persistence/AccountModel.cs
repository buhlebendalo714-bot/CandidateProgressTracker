using CandidateProgressTracker.Domain.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Persistence;
internal static class AccountModel
{
    internal static void Configure(ModelBuilder b)
    {
        var scope=b.Entity<CandidateProgressTracker.Domain.Learning.ScopeArchiveEvent>();scope.HasKey(x=>x.ScopeArchiveEventId);
        scope.Property(x=>x.ScopeType).HasMaxLength(20);scope.Property(x=>x.Reason).HasMaxLength(1000);scope.Property(x=>x.ExpectedVersion).HasMaxLength(100);
        scope.HasIndex(x=>new{x.ActorId,x.RequestId}).IsUnique();scope.HasIndex(x=>new{x.ScopeId,x.OccurredAt});
        scope.HasOne<IdentityUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);
        scope.ToTable("ScopeArchiveEvents",t=>{t.UseSqlOutputClause(false);t.HasCheckConstraint("CK_ScopeArchiveEvents_Kind","[ScopeType] IN ('Project','Cycle') AND LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 1000");});
        var audit=b.Entity<CandidateProgressTracker.Domain.Auditing.AuditEvent>();audit.HasKey(x=>x.AuditEventId);
        audit.Property(x=>x.ActorId).HasMaxLength(450);audit.Property(x=>x.EntityType).HasMaxLength(100);audit.Property(x=>x.EntityId).HasMaxLength(1000);audit.Property(x=>x.Action).HasMaxLength(80);audit.Property(x=>x.Details).HasMaxLength(2000);
        audit.HasIndex(x=>new{x.OccurredAt,x.AuditEventId});audit.HasIndex(x=>x.ActorId);audit.ToTable("AuditEvents",t=>t.UseSqlOutputClause(false));
        var s=b.Entity<AccountAccessState>();s.HasKey(x=>x.UserId);s.Property(x=>x.RowVersion).IsRowVersion();
        s.Property(x=>x.Reason).HasMaxLength(1000);s.HasOne<IdentityUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<IdentityUser>().WithMany().HasForeignKey(x=>x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        var c=b.Entity<AccountCredential>();c.HasKey(x=>x.CredentialId);c.Property(x=>x.Kind).HasMaxLength(20);c.Property(x=>x.TokenHash).HasMaxLength(64);
        c.HasIndex(x=>x.TokenHash).IsUnique();c.HasIndex(x=>new{x.UserId,x.IssuedAt});
        c.HasOne<IdentityUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        c.HasOne<IdentityUser>().WithMany().HasForeignKey(x=>x.IssuedBy).OnDelete(DeleteBehavior.Restrict);
        c.ToTable("AccountCredentials",t=>t.HasCheckConstraint("CK_AccountCredentials_Expiry","[ExpiresAt] > [IssuedAt] AND [Kind] IN ('Activation','Recovery')"));
        var a=b.Entity<AccountChange>();a.HasKey(x=>x.AccountChangeId);a.Property(x=>x.Action).HasMaxLength(80);a.Property(x=>x.Reason).HasMaxLength(1000);
        a.HasIndex(x=>x.OccurredAt);a.HasOne<IdentityUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<IdentityUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);
        a.ToTable("AccountChanges",t=>t.UseSqlOutputClause(false));
        b.Entity<IdentityUser>().HasIndex(u=>u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
    }
}
