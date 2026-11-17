namespace CandidateProgressTracker.Infrastructure.Persistence;
// Legacy-schema upgrade fixtures explicitly disable capture while seeding the
// predecessor. Normal application contexts always capture business writes.
public sealed class AuditCaptureOptions
{
    public bool Enabled { get; set; } = true;
}
