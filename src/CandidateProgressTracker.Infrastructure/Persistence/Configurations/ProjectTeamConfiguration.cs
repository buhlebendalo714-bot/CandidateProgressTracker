using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class ProjectTeamConfiguration
    : IEntityTypeConfiguration<ProjectTeam>
{
    public void Configure(EntityTypeBuilder<ProjectTeam> builder)
    {
        builder.ToTable("ProjectTeams");

        builder.HasKey(team => team.TeamId);

        builder.HasAlternateKey(team => new
        {
            team.TeamId,
            team.ProjectId
        });

        builder.Property(team => team.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(team => new
        {
            team.ProjectId,
            team.Name
        })
        .IsUnique();

        builder.Property(team => team.CreatedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(team => team.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(team => team.RowVersion)
            .IsRowVersion();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(team => team.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(team => team.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}