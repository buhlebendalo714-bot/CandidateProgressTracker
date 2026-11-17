using CandidateProgressTracker.Domain.Cohorts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;
internal static class OnboardingModel
{
    public static void Configure(ModelBuilder builder)
    {
        var batch = builder.Entity<OnboardingBatch>(); batch.HasKey(b => b.BatchId);
        batch.Property(b => b.ActorId).HasMaxLength(450); batch.Property(b => b.Fingerprint).HasMaxLength(64);
        batch.Property(b => b.PayloadHash).HasMaxLength(64);
        batch.HasOne<Cohort>().WithMany().HasForeignKey(b => b.CohortId).OnDelete(DeleteBehavior.Restrict);
        batch.HasOne<IdentityUser>().WithMany().HasForeignKey(b => b.ActorId).OnDelete(DeleteBehavior.Restrict);
        var row = builder.Entity<OnboardingBatchRow>(); row.HasKey(r => new {r.BatchId,r.Row});
        row.Property(r => r.UserName).HasMaxLength(256); row.Property(r => r.Email).HasMaxLength(256);
        row.Property(r => r.Action).HasMaxLength(20); row.Property(r => r.Status).HasMaxLength(20);
        row.Property(r => r.Message).HasMaxLength(1000); row.Property(r => r.ExpectedUserId).HasMaxLength(450);
        row.Property(r => r.UserId).HasMaxLength(450); row.Property(r => r.RowVersion).IsRowVersion();
        row.HasOne<OnboardingBatch>().WithMany().HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<CohortMembership>().WithMany().HasForeignKey(r => r.MembershipId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable("OnboardingBatchRows",t => {
            t.HasCheckConstraint("CK_OnboardingBatchRows_Action", "[Action] IN ('Create','EnrolExisting','Skip')");
            t.HasCheckConstraint("CK_OnboardingBatchRows_Status", "[Status] IN ('Pending','Failed','Created','Enrolled','Skipped')");
            t.HasCheckConstraint("CK_OnboardingBatchRows_Row", "[Row] > 1");
        });
    }
}
