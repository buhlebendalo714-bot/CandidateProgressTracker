using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.IntegrationTests;
internal class PreFilesDbContext(DbContextOptions<TrackerDbContext> options) : TrackerDbContext(options)
{
    protected override bool AuditCaptureEnabled => false;
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Ignore<EvidenceFile>(); b.Ignore<UploadAttempt>(); b.Ignore<CourseOffering>(); b.Ignore<Course>();
        b.Entity<Assignment>().Ignore(a=>a.OfferingId);
        b.Entity<Assignment>().Property(a=>a.ProjectId).IsRequired();
        b.Entity<Assignment>().Property(a=>a.MilestoneId).IsRequired();
        b.Entity<Assignment>().Metadata.RemoveCheckConstraint("CK_Assignments_Target");
        b.Entity<AssignmentCreditedRoster>().HasOne<Assignment>().WithMany().HasForeignKey(r=>new {r.AssignmentId,r.ProjectId})
            .HasPrincipalKey(a=>new {a.AssignmentId,a.ProjectId}).OnDelete(DeleteBehavior.Restrict);
    }
}
