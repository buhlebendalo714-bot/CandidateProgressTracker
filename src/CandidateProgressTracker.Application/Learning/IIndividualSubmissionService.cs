namespace CandidateProgressTracker.Application.Learning;

public record SubmissionEvidenceInput(
    string Label,
    string Url);

public record IndividualSubmissionRequest(
    Guid AssignmentId,
    Guid AssignmentRevisionId,
    Guid RequestId,
    string Explanation,
    IReadOnlyList<SubmissionEvidenceInput> Evidence);

public record SubmissionReceipt(
    Guid SubmissionId,
    int AttemptNo,
    DateTimeOffset ReceivedAt);

public record SubmissionResult(
    bool Succeeded,
    SubmissionReceipt? Receipt,
    IReadOnlyList<string> Errors);

public interface IIndividualSubmissionService
{
    Task<SubmissionResult> SubmitAsync(
        IndividualSubmissionRequest request,
        string userId);
}