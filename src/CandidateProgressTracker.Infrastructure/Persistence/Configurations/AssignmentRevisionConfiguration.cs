using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class AssignmentRevisionConfiguration
    : IEntityTypeConfiguration<AssignmentRevision>
{
    public void Configure(EntityTypeBuilder<AssignmentRevision> builder)
    {
        builder.ToTable("AssignmentRevisions", table =>
        {
            table.HasCheckConstraint(
                "CK_AssignmentRevisions_RevisionNo",
                "[RevisionNo] > 0");

            table.HasCheckConstraint(
                "CK_AssignmentRevisions_Reason",
                "LEN(LTRIM(RTRIM([Reason]))) > 0");

            table.HasCheckConstraint(
                "CK_AssignmentRevisions_UtcTimestamps",
                """
                DATEPART(TZOFFSET, [DueAt]) = 0
                AND DATEPART(TZOFFSET, [ChangedAt]) = 0
                """);
        });

        builder.HasKey(revision => revision.RevisionId);

        // Supports future submission references scoped to an assignment.
        builder.HasAlternateKey(revision => new
        {
            revision.RevisionId,
            revision.AssignmentId
        });

        builder.HasIndex(revision => new
        {
            revision.AssignmentId,
            revision.RevisionNo
        })
        .IsUnique();

        builder.Property(revision => revision.DueAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(revision => revision.InstructionsSnapshot)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(revision => revision.AcceptanceSnapshot)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(revision => revision.EvidencePolicySnapshot)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(revision => revision.ChangedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(revision => revision.ChangedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(revision => revision.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasOne<Assignment>()
            .WithMany()
            .HasForeignKey(revision => revision.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(revision => revision.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}