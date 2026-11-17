using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public sealed class IndividualSubmissionService
    : IIndividualSubmissionService
{
    private readonly TrackerDbContext _db;

    public IndividualSubmissionService(TrackerDbContext db)
    {
        _db = db;
    }

    public async Task<SubmissionResult> SubmitAsync(
        IndividualSubmissionRequest request,
        string userId)
    {
        var receivedAt = DateTimeOffset.UtcNow;

        if (request is null ||
            string.IsNullOrWhiteSpace(userId) ||
            request.AssignmentId == Guid.Empty ||
            request.AssignmentRevisionId == Guid.Empty ||
            request.RequestId == Guid.Empty)
        {
            return Fail("The submission request is invalid.");
        }

        var explanation = request.Explanation?.Trim() ?? string.Empty;

        if (explanation.Length is < 1 or > 10000)
        {
            return Fail("Enter an explanation of 1–10,000 characters.");
        }

        if (request.Evidence is null ||
            request.Evidence.Count is < 1 or > 10)
        {
            return Fail("Provide between 1 and 10 evidence links.");
        }

        var evidence = new List<SubmissionEvidenceInput>();
        var urls = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in request.Evidence)
        {
            if (item is null)
            {
                return Fail("An evidence item is invalid.");
            }

            var label = item.Label?.Trim() ?? string.Empty;
            var url = item.Url?.Trim() ?? string.Empty;

            if (label.Length is < 1 or > 200)
            {
                return Fail("Each evidence label must contain 1–200 characters.");
            }

            if (url.Length is < 1 or > 2000 ||
                url.Any(char.IsWhiteSpace) ||
                url.Any(char.IsControl) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                return Fail(
                    "Each evidence link must be an absolute HTTPS URL " +
                    "without embedded credentials or whitespace.");
            }

            if (!urls.Add(url))
            {
                return Fail("Do not include the same evidence link twice.");
            }

            evidence.Add(new SubmissionEvidenceInput(label, url));
        }

        Submission? addedSubmission = null;
        Assignment? touchedAssignment = null;
        var addedEvidence = new List<SubmissionEvidence>();
        var committed = false;

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            await AssignmentWorkflow.LockAsync(_db, request.AssignmentId);
            var now = DateTimeOffset.UtcNow;

            var target = await AssignmentAccess.TargetAsync(_db, request.AssignmentId);
            var accessible = target is not null && target.Assignment.TeamId is null
                && await AssignmentAccess.CandidateAsync(_db, target, userId, true);

            if (!accessible)
            {
                return Fail("This assignment is not available for submission.");
            }

            // Check retries before checking the latest revision:
            // an accepted request remains identifiable after a revision change.
            if (await _db.UploadAttempts.AnyAsync(a => a.ActorId == userId && a.RequestId == request.RequestId))
                return Fail("This form has already started a file upload. Open its processing page to check the result.");
            var previous = await _db.Submissions
                .AsNoTracking()
                .SingleOrDefaultAsync(submission =>
                    submission.SubmittedBy == userId &&
                    submission.RequestId == request.RequestId);

            if (previous is not null)
            {
                var previousEvidence = await _db.SubmissionEvidence
                    .AsNoTracking()
                    .Where(item =>
                        item.SubmissionId == previous.SubmissionId)
                    .OrderBy(item => item.SequenceNo)
                    .Select(item => new SubmissionEvidenceInput(
                        item.Label,
                        item.Url))
                    .ToListAsync();

                if (previous.AssignmentId != request.AssignmentId ||
                    previous.AssignmentRevisionId != request.AssignmentRevisionId ||
                    previous.Explanation != explanation ||
                    !previousEvidence.SequenceEqual(evidence))
                {
                    return Fail(
                        "This form was already used for different content. Reload it before submitting your changes.");
                }

                await transaction.CommitAsync();

                return Success(previous);
            }

            if (await _db.UploadAttempts.AnyAsync(a => a.ActorId == userId && a.RequestId == request.RequestId))
                return Fail("This form has already started a file upload. Open its processing page to check the result.");
            if (target!.Assignment.OfferingId is not null) return Fail("Courses require an actual certificate file.");
            var workflow = await AssignmentWorkflow.LoadAsync(_db, request.AssignmentId);
            if (workflow.EffectiveApproval is not null)
                return Fail("This assignment is approved. Ask your Administrator if its approval needs to be reopened before submitting again.");

            var latestRevisionId = await _db.AssignmentRevisions
                .AsNoTracking()
                .Where(revision =>
                    revision.AssignmentId == request.AssignmentId)
                .OrderByDescending(revision => revision.RevisionNo)
                .Select(revision => (Guid?)revision.RevisionId)
                .FirstOrDefaultAsync();

            if (latestRevisionId != request.AssignmentRevisionId)
            {
                return Fail(
                    "The assignment requirements have changed. " +
                    "Reload them before submitting.");
            }

            var lastAttempt = await _db.Submissions
                .Where(submission =>
                    submission.AssignmentId == request.AssignmentId)
                .Select(submission => (int?)submission.AttemptNo)
                .MaxAsync() ?? 0;

            if (lastAttempt == int.MaxValue)
            {
                return Fail("No further submission attempts can be recorded.");
            }

            var recordedAt = DateTimeOffset.UtcNow;

            addedSubmission = new Submission
            {
                AssignmentId = request.AssignmentId,
                AssignmentRevisionId = request.AssignmentRevisionId,
                AttemptNo = lastAttempt + 1,
                RequestId = request.RequestId,
                Explanation = explanation,
                SubmittedBy = userId,
                ReceivedAt = receivedAt,
                RecordedAt = recordedAt < receivedAt
                    ? receivedAt
                    : recordedAt
            };

            for (var index = 0; index < evidence.Count; index++)
            {
                addedEvidence.Add(new SubmissionEvidence
                {
                    SubmissionId = addedSubmission.SubmissionId,
                    SequenceNo = index + 1,
                    Label = evidence[index].Label,
                    Url = evidence[index].Url
                });
            }

            _db.Submissions.Add(addedSubmission);
            _db.SubmissionEvidence.AddRange(addedEvidence);

            touchedAssignment = await AssignmentWorkflow.TouchAsync(_db, request.AssignmentId);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            committed = true;

            return Success(addedSubmission);
        }
        catch (Exception exception) when (IsRetryable(exception))
        {
            return Retry();
        }
        finally
        {
            if (!committed && addedSubmission is not null)
            {
                foreach (var item in addedEvidence)
                {
                    _db.Entry(item).State = EntityState.Detached;
                }

                _db.Entry(addedSubmission).State = EntityState.Detached;
                if (touchedAssignment is not null) _db.Entry(touchedAssignment).State = EntityState.Detached;
            }
        }
    }

    // EF's SQL Server execution strategy can wrap a deadlock in an
    // InvalidOperationException, with a DbUpdateException beneath it on writes.
    // Unwrap only these known wrappers, and only handle the SQL errors for which
    // the caller can safely retry the same request ID.
    private static bool IsRetryable(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException sql => sql.Number is -2 or 1205 or 2601 or 2627 or 51010,
        DbUpdateException or InvalidOperationException =>
            exception.InnerException is not null && IsRetryable(exception.InnerException),
        _ => false
    };

    private static SubmissionResult Retry() =>
        Fail(
            "Another request changed the submission records. " +
            "Retry using the same request ID.");

    private static SubmissionResult Fail(string error) =>
        new(false, null, new[] { error });

    private static SubmissionResult Success(Submission submission) =>
        new(
            true,
            new SubmissionReceipt(
                submission.SubmissionId,
                submission.AttemptNo,
                submission.ReceivedAt),
            Array.Empty<string>());
}
