using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects", table =>
        {
            table.HasCheckConstraint(
                "CK_Projects_Mode",
                "[Mode] IN (1, 2)");

            table.HasCheckConstraint(
                "CK_Projects_ArchiveDetails",
                """
                ([IsArchived] = 0 AND [ArchivedAt] IS NULL
                    AND [ArchivedBy] IS NULL AND [ArchiveReason] IS NULL)
                OR
                ([IsArchived] = 1 AND [ArchivedAt] IS NOT NULL
                    AND [ArchivedBy] IS NOT NULL
                    AND [ArchiveReason] IS NOT NULL
                    AND LEN(LTRIM(RTRIM([ArchiveReason]))) > 0)
                """);
        });

        builder.HasKey(project => project.ProjectId);

        // Supports future assignment references that must match the cycle.
        builder.HasAlternateKey(project => new
        {
            project.ProjectId,
            project.CycleId
        });

        builder.Property(project => project.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.Brief)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(project => project.Mode)
            .HasConversion<byte>()
            .HasColumnType("tinyint");

        builder.Property(project => project.IsArchived)
            .HasDefaultValue(false);

        builder.Property(project => project.ArchivedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(project => project.ArchivedBy)
            .HasMaxLength(450);

        builder.Property(project => project.ArchiveReason)
            .HasMaxLength(1000);

        builder.Property(project => project.CreatedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(project => project.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(project => project.RowVersion)
            .IsRowVersion();

        builder.HasOne<LearningCycle>()
            .WithMany()
            .HasForeignKey(project => project.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(project => project.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(project => project.ArchivedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}