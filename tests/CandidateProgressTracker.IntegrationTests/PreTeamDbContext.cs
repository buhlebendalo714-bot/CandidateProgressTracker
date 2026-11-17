using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.IntegrationTests;

// Seed/read the historical schema with its actual column shape. Migrations are
// always run through the current TrackerDbContext, never this legacy mapping.
internal sealed class PreTeamDbContext(DbContextOptions<TrackerDbContext> options) : PreFilesDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        var entity = builder.Entity<Submission>().Metadata;
        foreach (var fk in entity.GetForeignKeys().Where(f => f.PrincipalEntityType.ClrType == typeof(AssignmentCreditedRoster)).ToList())
            entity.RemoveForeignKey(fk);
        builder.Ignore<AssignmentCreditedRosterMember>();
        builder.Ignore<AssignmentCreditedRoster>();
        builder.Entity<Submission>().Ignore(s => s.CreditedRosterAssignmentId);
        builder.Entity<Submission>().Metadata.RemoveCheckConstraint("CK_Submissions_RosterAssignment");
    }
}
