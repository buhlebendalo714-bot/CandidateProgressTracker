using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class TeamMembershipConfiguration
    : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("TeamMemberships", table =>
        {
            table.HasCheckConstraint(
                "CK_TeamMemberships_DateOrder",
                "[EndsAt] IS NULL OR [EndsAt] > [StartsAt]");
        });

        builder.HasKey(membership => membership.TeamMembershipId);

        builder.Property(membership => membership.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(membership => membership.StartsAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(membership => membership.EndsAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(membership => membership.ChangeReason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(membership => membership.ChangedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(membership => membership.RowVersion)
            .IsRowVersion();

        builder.HasIndex(membership => new
        {
            membership.ProjectId,
            membership.UserId
        })
        .IsUnique()
        .HasFilter("[EndsAt] IS NULL");

        // Ensures the referenced team belongs to the stated project.
        builder.HasOne<ProjectTeam>()
            .WithMany()
            .HasForeignKey(membership => new
            {
                membership.TeamId,
                membership.ProjectId
            })
            .HasPrincipalKey(team => new
            {
                team.TeamId,
                team.ProjectId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(membership => membership.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(membership => membership.EndedBy)
            .HasMaxLength(450);

        builder.Property(membership => membership.EndReason)
            .HasMaxLength(1000);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(membership => membership.EndedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}