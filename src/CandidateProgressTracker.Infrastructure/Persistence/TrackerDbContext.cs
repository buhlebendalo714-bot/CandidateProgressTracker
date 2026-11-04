using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Persistence;

public class TrackerDbContext : IdentityDbContext<IdentityUser>
{
    public TrackerDbContext(DbContextOptions<TrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cohort> Cohorts => Set<Cohort>();

    public DbSet<CohortMembership> CohortMemberships =>
        Set<CohortMembership>();
        
    public DbSet<LearningCycle> LearningCycles => 
        Set<LearningCycle>();

    public DbSet<Project> Projects => 
        Set<Project>();

    public DbSet<Milestone> Milestones => 
        Set<Milestone>();

    public DbSet<ProjectTeam> ProjectTeams => 
        Set<ProjectTeam>();

    public DbSet<TeamMembership> TeamMemberships => 
        Set<TeamMembership>();

    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<AssignmentRevision> AssignmentRevisions =>
        Set<AssignmentRevision>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new CohortConfiguration());
        builder.ApplyConfiguration(new CohortMembershipConfiguration());
        builder.ApplyConfiguration(new LearningCycleConfiguration());
        builder.ApplyConfiguration(new ProjectConfiguration());
        builder.ApplyConfiguration(new MilestoneConfiguration());
        builder.ApplyConfiguration(new ProjectTeamConfiguration());
        builder.ApplyConfiguration(new TeamMembershipConfiguration());
        builder.ApplyConfiguration(new AssignmentConfiguration());
        builder.ApplyConfiguration(new AssignmentRevisionConfiguration());
}
}