using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones", table =>
        {
            table.HasCheckConstraint(
                "CK_Milestones_SequenceNo",
                "[SequenceNo] > 0");
        });

        builder.HasKey(milestone => milestone.MilestoneId);

        builder.HasAlternateKey(milestone => new
        {
            milestone.MilestoneId,
            milestone.ProjectId
        });

        builder.HasIndex(milestone => new
        {
            milestone.ProjectId,
            milestone.SequenceNo
        })
        .IsUnique();

        builder.Property(milestone => milestone.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(milestone => milestone.Instructions)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(milestone => milestone.AcceptanceExpectations)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(milestone => milestone.EvidencePolicy)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(milestone => milestone.CreatedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(milestone => milestone.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(milestone => milestone.RowVersion)
            .IsRowVersion();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(milestone => milestone.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(milestone => milestone.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}