using CandidateProgressTracker.Domain.Cohorts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class CohortMembershipConfiguration
    : IEntityTypeConfiguration<CohortMembership>
{
    public void Configure(EntityTypeBuilder<CohortMembership> builder)
    {
        builder.ToTable("CohortMemberships", table =>
        {
            table.HasCheckConstraint(
                "CK_CohortMemberships_DateOrder",
                "[EndsAt] IS NULL OR [EndsAt] > [StartsAt]");

            table.HasCheckConstraint(
                "CK_CohortMemberships_Kind",
                "[MembershipKind] IN (1, 2)");
        });

        builder.HasKey(membership => membership.MembershipId);

        builder.Property(membership => membership.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(membership => membership.MembershipKind)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(membership => membership.StartsAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(membership => membership.EndsAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(membership => membership.ChangeReason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(membership => membership.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(membership => membership.RowVersion)
            .IsRowVersion();

        builder.HasOne<Cohort>()
            .WithMany()
            .HasForeignKey(membership => membership.CohortId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(membership => membership.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new
        {
            membership.CohortId,
            membership.UserId,
            membership.MembershipKind
        })
            .IsUnique()
            .HasFilter("[EndsAt] IS NULL");

        builder.Property(membership => membership.EndedBy)
            .HasMaxLength(450);

        builder.Property(membership => membership.EndReason)
            .HasMaxLength(1000);

        builder.Property(membership => membership.EndedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(membership => membership.EndedBy)
            .OnDelete(DeleteBehavior.Restrict);  
    }
}