using CandidateProgressTracker.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class SubmissionEvidenceConfiguration
    : IEntityTypeConfiguration<SubmissionEvidence>
{
    public void Configure(EntityTypeBuilder<SubmissionEvidence> builder)
    {
        builder.ToTable("SubmissionEvidence", table =>
        {
            table.HasCheckConstraint(
                "CK_SubmissionEvidence_SequenceNo",
                "[SequenceNo] > 0");

            table.HasCheckConstraint(
                "CK_SubmissionEvidence_Label",
                "LEN(LTRIM(RTRIM([Label]))) > 0");

            table.HasCheckConstraint(
                "CK_SubmissionEvidence_Url",
                "LEN(LTRIM(RTRIM([Url]))) > 0");
        });

        builder.HasKey(evidence => evidence.SubmissionEvidenceId);

        builder.HasIndex(evidence => new
        {
            evidence.SubmissionId,
            evidence.SequenceNo
        })
        .IsUnique();

        builder.Property(evidence => evidence.Label)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(evidence => evidence.Url)
            .HasMaxLength(2000)
            .IsRequired();

        builder.HasOne<Submission>()
            .WithMany()
            .HasForeignKey(evidence => evidence.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}