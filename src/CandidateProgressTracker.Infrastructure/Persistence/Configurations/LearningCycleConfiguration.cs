using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class LearningCycleConfiguration
    : IEntityTypeConfiguration<LearningCycle>
{
    public void Configure(EntityTypeBuilder<LearningCycle> builder)
    {
        builder.ToTable("LearningCycles", table =>
        {
            table.HasCheckConstraint(
                "CK_LearningCycles_DateOrder",
                "[EndsOn] >= [StartsOn]");

            table.HasCheckConstraint(
                "CK_LearningCycles_ArchiveDetails",
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

        builder.HasKey(cycle => cycle.CycleId);

        builder.Property(cycle => cycle.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(cycle => new { cycle.CohortId, cycle.Name })
            .IsUnique();

        builder.Property(cycle => cycle.StartsOn)
            .HasColumnType("date");

        builder.Property(cycle => cycle.EndsOn)
            .HasColumnType("date");

        builder.Property(cycle => cycle.IsArchived)
            .HasDefaultValue(false);

        builder.Property(cycle => cycle.ArchivedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(cycle => cycle.ArchivedBy)
            .HasMaxLength(450);

        builder.Property(cycle => cycle.ArchiveReason)
            .HasMaxLength(1000);

        builder.Property(cycle => cycle.CreatedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(cycle => cycle.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(cycle => cycle.RowVersion)
            .IsRowVersion();

        builder.HasOne<Cohort>()
            .WithMany()
            .HasForeignKey(cycle => cycle.CohortId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(cycle => cycle.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(cycle => cycle.ArchivedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}