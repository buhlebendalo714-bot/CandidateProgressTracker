using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Persistence;
internal static class EvidenceModel
{
    internal static void Configure(ModelBuilder b)
    {
        var a = b.Entity<UploadAttempt>(); a.HasKey(x => x.UploadAttemptId);
        a.Property(x => x.RowVersion).IsRowVersion(); a.Property(x => x.ActorId).HasMaxLength(450);
        a.Property(x => x.Status).HasMaxLength(30); a.Property(x => x.Message).HasMaxLength(1000);
        a.Property(x => x.PayloadHash).HasMaxLength(64); a.Property(x => x.ExpectedVersion).HasMaxLength(8);
        a.Property(x => x.RosterConfirmation).HasMaxLength(64); a.Property(x => x.Explanation).HasMaxLength(10000);
        a.HasIndex(x => new { x.ActorId, x.RequestId }).IsUnique();
        a.HasIndex(x => new { x.Status, x.NextTryAt, x.LeaseUntil });
        a.HasIndex(x => x.SubmissionId).IsUnique().HasFilter("[SubmissionId] IS NOT NULL");
        a.HasOne<AssignmentRevision>().WithMany().HasForeignKey(x => new { x.RevisionId, x.AssignmentId }).HasPrincipalKey(x => new { x.RevisionId, x.AssignmentId }).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<Submission>().WithMany().HasForeignKey(x => new { x.SubmissionId, x.AssignmentId }).HasPrincipalKey(x => new { x.SubmissionId, x.AssignmentId }).OnDelete(DeleteBehavior.Restrict);
        a.ToTable("UploadAttempts", t => { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_UploadAttempts_State", "([Status]='Committed' AND [SubmissionId] IS NOT NULL) OR ([Status] IN ('Processing','Rejected','Expired') AND [SubmissionId] IS NULL)"); });
        var f = b.Entity<EvidenceFile>(); f.HasKey(x => x.EvidenceFileId);
        f.HasOne<UploadAttempt>().WithMany().HasForeignKey(x => x.UploadAttemptId).OnDelete(DeleteBehavior.Restrict);
        f.Property(x => x.OriginalName).HasMaxLength(255); f.Property(x => x.ObjectKey).HasMaxLength(64); f.Property(x => x.MediaType).HasMaxLength(40);
        f.Property(x => x.Sha256).HasMaxLength(64); f.Property(x => x.ScanResult).HasMaxLength(30);
        f.HasIndex(x => x.ObjectKey).IsUnique(); f.HasIndex(x => new { x.UploadAttemptId, x.SequenceNo }).IsUnique();
        f.ToTable("EvidenceFiles", t => { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_EvidenceFiles_Size", "[SizeBytes]>0 AND [SizeBytes]<=10485760 AND [SequenceNo] BETWEEN 1 AND 5"); });
    }
}
