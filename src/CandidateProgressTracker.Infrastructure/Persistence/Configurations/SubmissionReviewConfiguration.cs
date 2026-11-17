using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public sealed class SubmissionReviewConfiguration : IEntityTypeConfiguration<SubmissionReview>
{
    public void Configure(EntityTypeBuilder<SubmissionReview> b)
    {
        b.ToTable("SubmissionReviews", t =>
        {
            t.HasCheckConstraint("CK_SubmissionReviews_Decision", "[Decision] IN (1, 2)");
            t.HasCheckConstraint("CK_SubmissionReviews_Feedback", "DATALENGTH([Feedback]) <= 20000 AND ([Decision] <> 2 OR LEN(LTRIM(RTRIM([Feedback]))) > 0)");
            t.HasCheckConstraint("CK_SubmissionReviews_Number", "[ReviewNo] > 0");
            t.HasCheckConstraint("CK_SubmissionReviews_Request", "[RequestId] <> '00000000-0000-0000-0000-000000000000'");
            t.HasCheckConstraint("CK_SubmissionReviews_Utc", "DATEPART(TZOFFSET, [DecidedAt]) = 0");
        });
        b.HasKey(r => r.ReviewId);
        b.HasAlternateKey(r => new { r.ReviewId, r.AssignmentId });
        b.HasIndex(r => new { r.AssignmentId, r.ReviewNo }).IsUnique();
        b.HasIndex(r => new { r.ReviewerId, r.RequestId }).IsUnique();
        b.Property(r => r.Decision).HasConversion<byte>();
        b.Property(r => r.Feedback).HasMaxLength(10000).IsRequired();
        b.Property(r => r.ReviewerId).HasMaxLength(450).IsRequired();
        b.HasOne<Submission>().WithMany().HasForeignKey(r => new { r.SubmissionId, r.AssignmentId })
            .HasPrincipalKey(s => new { s.SubmissionId, s.AssignmentId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssignmentReopeningConfiguration : IEntityTypeConfiguration<AssignmentReopening>
{
    public void Configure(EntityTypeBuilder<AssignmentReopening> b)
    {
        b.ToTable("AssignmentReopenings", t =>
        {
            t.HasCheckConstraint("CK_AssignmentReopenings_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
            t.HasCheckConstraint("CK_AssignmentReopenings_Request", "[RequestId] <> '00000000-0000-0000-0000-000000000000'");
            t.HasCheckConstraint("CK_AssignmentReopenings_Utc", "DATEPART(TZOFFSET, [ReopenedAt]) = 0");
        });
        b.HasKey(r => r.ReopeningId);
        b.HasIndex(r => r.ApprovalReviewId).IsUnique();
        b.HasIndex(r => new { r.AdministratorId, r.RequestId }).IsUnique();
        b.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        b.Property(r => r.AdministratorId).HasMaxLength(450).IsRequired();
        b.HasOne<SubmissionReview>().WithMany().HasForeignKey(r => new { ReviewId = r.ApprovalReviewId, r.AssignmentId })
            .HasPrincipalKey(r => new { r.ReviewId, r.AssignmentId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.AdministratorId).OnDelete(DeleteBehavior.Restrict);
    }
}
