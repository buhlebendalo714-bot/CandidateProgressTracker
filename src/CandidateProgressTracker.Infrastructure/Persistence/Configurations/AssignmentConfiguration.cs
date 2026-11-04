using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments", table =>
        {
            table.HasCheckConstraint(
                "CK_Assignments_ExactlyOneOwner",
                """
                ([CandidateId] IS NOT NULL AND [TeamId] IS NULL)
                OR
                ([CandidateId] IS NULL AND [TeamId] IS NOT NULL)
                """);

            table.HasCheckConstraint(
                "CK_Assignments_UtcTimestamps",
                """
                DATEPART(TZOFFSET, [DueAt]) = 0
                AND DATEPART(TZOFFSET, [CreatedAt]) = 0
                """);
        });

        builder.HasKey(assignment => assignment.AssignmentId);

        builder.Property(assignment => assignment.CandidateId)
            .HasMaxLength(450);

        builder.Property(assignment => assignment.DueAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(assignment => assignment.CreatedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(assignment => assignment.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(assignment => assignment.RowVersion)
            .IsRowVersion();

        // One assignment per milestone and individual owner.
        builder.HasIndex(assignment => new
        {
            assignment.MilestoneId,
            assignment.CandidateId
        })
        .IsUnique()
        .HasFilter("[CandidateId] IS NOT NULL");

        // One assignment per milestone and team owner.
        builder.HasIndex(assignment => new
        {
            assignment.MilestoneId,
            assignment.TeamId
        })
        .IsUnique()
        .HasFilter("[TeamId] IS NOT NULL");

        builder.HasIndex(assignment => new
        {
            assignment.CycleId,
            assignment.CandidateId,
            assignment.DueAt
        });

        builder.HasIndex(assignment => new
        {
            assignment.TeamId,
            assignment.DueAt
        });

        // The project must belong to the stated cycle.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.ProjectId,
                assignment.CycleId
            })
            .HasPrincipalKey(project => new
            {
                project.ProjectId,
                project.CycleId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // The milestone must belong to the stated project.
        builder.HasOne<Milestone>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.MilestoneId,
                assignment.ProjectId
            })
            .HasPrincipalKey(milestone => new
            {
                milestone.MilestoneId,
                milestone.ProjectId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // A team owner must belong to the same project.
        builder.HasOne<ProjectTeam>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.TeamId,
                assignment.ProjectId
            })
            .HasPrincipalKey(team => new
            {
                team.TeamId,
                team.ProjectId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}