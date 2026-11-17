using CandidateProgressTracker.Domain.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateProgressTracker.Infrastructure.Persistence.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_Submissions_RosterAssignment", "[CreditedRosterAssignmentId] IS NULL OR [CreditedRosterAssignmentId] = [AssignmentId]");
            table.HasCheckConstraint(
                "CK_Submissions_AttemptNo",
                "[AttemptNo] > 0");

            table.HasCheckConstraint(
                "CK_Submissions_RequestId",
                "[RequestId] <> '00000000-0000-0000-0000-000000000000'");

            table.HasCheckConstraint(
                "CK_Submissions_Explanation",
                "LEN(LTRIM(RTRIM([Explanation]))) > 0");

            table.HasCheckConstraint(
                "CK_Submissions_UtcTimestamps",
                """
                DATEPART(TZOFFSET, [ReceivedAt]) = 0
                AND DATEPART(TZOFFSET, [RecordedAt]) = 0
                """);

            table.HasCheckConstraint(
                "CK_Submissions_TimestampOrder",
                "[RecordedAt] >= [ReceivedAt]");
        });

        builder.HasKey(submission => submission.SubmissionId);
        builder.HasOne<AssignmentCreditedRoster>().WithMany().HasForeignKey(s => s.CreditedRosterAssignmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(submission => new
        {
            submission.AssignmentId,
            submission.AttemptNo
        })
        .IsUnique();

        builder.HasIndex(submission => new
        {
            submission.SubmittedBy,
            submission.RequestId
        })
        .IsUnique();

        builder.Property(submission => submission.Explanation)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(submission => submission.SubmittedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(submission => submission.ReceivedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(submission => submission.RecordedAt)
            .HasColumnType("datetimeoffset(7)");

        // The referenced revision must belong to this assignment.
        builder.HasOne<AssignmentRevision>()
            .WithMany()
            .HasForeignKey(submission => new
            {
                submission.AssignmentRevisionId,
                submission.AssignmentId
            })
            .HasPrincipalKey(revision => new
            {
                revision.RevisionId,
                revision.AssignmentId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(submission => submission.SubmittedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
