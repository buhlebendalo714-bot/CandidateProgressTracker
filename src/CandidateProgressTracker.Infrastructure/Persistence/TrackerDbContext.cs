using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Persistence;

public class TrackerDbContext : IdentityDbContext<IdentityUser>
{
    public DbSet<CandidateProgressTracker.Domain.Accounts.AccountAccessState> AccountAccessStates => Set<CandidateProgressTracker.Domain.Accounts.AccountAccessState>();
    public DbSet<CandidateProgressTracker.Domain.Accounts.AccountCredential> AccountCredentials => Set<CandidateProgressTracker.Domain.Accounts.AccountCredential>();
    public DbSet<CandidateProgressTracker.Domain.Accounts.AccountChange> AccountChanges => Set<CandidateProgressTracker.Domain.Accounts.AccountChange>();
    private readonly AuditCaptureOptions _auditOptions;
    public string? AuditActorId { get; set; }
    protected virtual bool AuditCaptureEnabled => _auditOptions.Enabled;
    public DbSet<CandidateProgressTracker.Domain.Auditing.AuditEvent> AuditEvents => Set<CandidateProgressTracker.Domain.Auditing.AuditEvent>();
    public TrackerDbContext(DbContextOptions<TrackerDbContext> options, AuditCaptureOptions? auditOptions = null)
        : base(options)
    {
        _auditOptions = auditOptions ?? new();
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var audit = AuditCaptureEnabled ? AuditCapture.Prepare(this) : [];
        AuditEvents.AddRange(audit);
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch { foreach(var row in audit) Entry(row).State = EntityState.Detached; throw; }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var audit = AuditCaptureEnabled ? AuditCapture.Prepare(this) : [];
        AuditEvents.AddRange(audit);
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch { foreach(var row in audit) Entry(row).State = EntityState.Detached; throw; }
    }

    public DbSet<OnboardingBatch> OnboardingBatches => Set<OnboardingBatch>();
    public DbSet<OnboardingBatchRow> OnboardingBatchRows => Set<OnboardingBatchRow>();

    public DbSet<Cohort> Cohorts => Set<Cohort>();

    public DbSet<CohortMembership> CohortMemberships =>
        Set<CohortMembership>();

    public DbSet<LearningCycle> LearningCycles =>
        Set<LearningCycle>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Milestone> Milestones => Set<Milestone>();

    public DbSet<ProjectTeam> ProjectTeams => Set<ProjectTeam>();

    public DbSet<TeamMembership> TeamMemberships =>
        Set<TeamMembership>();

    public DbSet<UploadAttempt> UploadAttempts => Set<UploadAttempt>();
    public DbSet<EvidenceFile> EvidenceFiles => Set<EvidenceFile>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();
    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<AssignmentRevision> AssignmentRevisions =>
        Set<AssignmentRevision>();

    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<AssignmentCreditedRoster> AssignmentCreditedRosters => Set<AssignmentCreditedRoster>();
    public DbSet<AssignmentCreditedRosterMember> AssignmentCreditedRosterMembers => Set<AssignmentCreditedRosterMember>();
    public DbSet<SubmissionReview> SubmissionReviews => Set<SubmissionReview>();
    public DbSet<AssignmentReopening> AssignmentReopenings => Set<AssignmentReopening>();
    public DbSet<ScopeArchiveEvent> ScopeArchiveEvents => Set<ScopeArchiveEvent>();

    public DbSet<SubmissionEvidence> SubmissionEvidence =>
        Set<SubmissionEvidence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        AccountModel.Configure(builder);
        OnboardingModel.Configure(builder);
        var course = builder.Entity<Course>();
        course.HasKey(c => c.CourseId);
        course.Property(c => c.Title).HasMaxLength(200); course.Property(c => c.Platform).HasMaxLength(120); course.Property(c => c.Url).HasMaxLength(2048);
        course.HasOne<IdentityUser>().WithMany().HasForeignKey(c => c.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        var offering = builder.Entity<CourseOffering>(); offering.HasKey(o => o.OfferingId);
        offering.HasAlternateKey(o => new { o.OfferingId, o.CycleId });
        offering.HasIndex(o => new { o.CourseId, o.CycleId }).IsUnique();
        offering.Property(o => o.TitleSnapshot).HasMaxLength(200); offering.Property(o => o.PlatformSnapshot).HasMaxLength(120); offering.Property(o => o.UrlSnapshot).HasMaxLength(2048);
        offering.HasOne<Course>().WithMany().HasForeignKey(o => o.CourseId).OnDelete(DeleteBehavior.Restrict);
        offering.HasOne<LearningCycle>().WithMany().HasForeignKey(o => o.CycleId).OnDelete(DeleteBehavior.Restrict);
        offering.HasOne<IdentityUser>().WithMany().HasForeignKey(o => o.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        EvidenceModel.Configure(builder);

        builder.ApplyConfiguration(new CohortConfiguration());
        builder.ApplyConfiguration(new CohortMembershipConfiguration());
        builder.ApplyConfiguration(new LearningCycleConfiguration());
        builder.ApplyConfiguration(new ProjectConfiguration());
        builder.ApplyConfiguration(new MilestoneConfiguration());
        builder.ApplyConfiguration(new ProjectTeamConfiguration());
        builder.ApplyConfiguration(new TeamMembershipConfiguration());
        builder.ApplyConfiguration(new AssignmentConfiguration());
        builder.ApplyConfiguration(new AssignmentRevisionConfiguration());
        builder.ApplyConfiguration(new SubmissionConfiguration());
        builder.ApplyConfiguration(new AssignmentCreditedRosterConfiguration());
        builder.ApplyConfiguration(new AssignmentCreditedRosterMemberConfiguration());
        builder.ApplyConfiguration(new SubmissionReviewConfiguration());
        builder.ApplyConfiguration(new AssignmentReopeningConfiguration());
        builder.ApplyConfiguration(new SubmissionEvidenceConfiguration());
    }
}
