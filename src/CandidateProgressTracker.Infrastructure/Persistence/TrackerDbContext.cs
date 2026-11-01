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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new CohortConfiguration());
        builder.ApplyConfiguration(new CohortMembershipConfiguration());
        builder.ApplyConfiguration(new LearningCycleConfiguration());
        builder.ApplyConfiguration(new ProjectConfiguration());
    }
}