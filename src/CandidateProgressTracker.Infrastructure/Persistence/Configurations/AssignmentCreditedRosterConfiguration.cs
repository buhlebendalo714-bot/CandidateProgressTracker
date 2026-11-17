using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public sealed class AssignmentCreditedRosterConfiguration : IEntityTypeConfiguration<AssignmentCreditedRoster>
{
    public void Configure(EntityTypeBuilder<AssignmentCreditedRoster> b)
    {
        b.ToTable("AssignmentCreditedRosters", t =>
        {
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_CreditedRosters_Utc", "DATEPART(TZOFFSET, [FrozenAt]) = 0");
        });
        b.HasKey(r => r.AssignmentId);
        b.HasAlternateKey(r => new { r.AssignmentId, r.TeamId, r.ProjectId, r.CohortId });
        b.Property(r => r.TeamNameSnapshot).HasMaxLength(150).IsRequired();
        b.Property(r => r.FrozenBy).HasMaxLength(450).IsRequired();
        b.HasOne<Assignment>().WithMany().HasForeignKey(r => r.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectTeam>().WithMany().HasForeignKey(r => new { r.TeamId, r.ProjectId })
            .HasPrincipalKey(t => new { t.TeamId, t.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Cohort>().WithMany().HasForeignKey(r => r.CohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.FrozenBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssignmentCreditedRosterMemberConfiguration : IEntityTypeConfiguration<AssignmentCreditedRosterMember>
{
    public void Configure(EntityTypeBuilder<AssignmentCreditedRosterMember> b)
    {
        b.ToTable("AssignmentCreditedRosterMembers", t => t.UseSqlOutputClause(false));
        b.HasKey(r => new { r.AssignmentId, r.UserId });
        b.Property(r => r.UserId).HasMaxLength(450);
        b.Property(r => r.UserNameSnapshot).HasMaxLength(256).IsRequired();
        b.HasOne<AssignmentCreditedRoster>().WithMany().HasForeignKey(r => new { r.AssignmentId, r.TeamId, r.ProjectId, r.CohortId })
            .HasPrincipalKey(r => new { r.AssignmentId, r.TeamId, r.ProjectId, r.CohortId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TeamMembership>().WithMany().HasForeignKey(r => new { r.TeamMembershipId, r.TeamId, r.ProjectId, r.UserId })
            .HasPrincipalKey(m => new { m.TeamMembershipId, m.TeamId, m.ProjectId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CohortMembership>().WithMany().HasForeignKey(r => new { MembershipId = r.CohortMembershipId, r.CohortId, r.UserId })
            .HasPrincipalKey(m => new { m.MembershipId, m.CohortId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
