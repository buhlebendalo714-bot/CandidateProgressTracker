using CandidateProgressTracker.Domain.Cohorts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class CohortConfiguration : IEntityTypeConfiguration<Cohort>
{
    public void Configure(EntityTypeBuilder<Cohort> builder)
    {
        builder.ToTable("Cohorts");

        builder.HasKey(cohort => cohort.CohortId);

        builder.Property(cohort => cohort.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(cohort => cohort.Code)
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(cohort => cohort.Code)
            .IsUnique();

        builder.Property(cohort => cohort.LocationLabel)
            .HasMaxLength(120);

        builder.Property(cohort => cohort.IsArchived)
            .HasDefaultValue(false);

        builder.Property(cohort => cohort.ArchivedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(cohort => cohort.ArchivedBy)
            .HasMaxLength(450);

        builder.Property(cohort => cohort.ArchiveReason)
            .HasMaxLength(1000);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(cohort => cohort.ArchivedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(cohort => cohort.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(cohort => cohort.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(cohort => cohort.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(cohort => cohort.RowVersion)
            .IsRowVersion();
    }
}