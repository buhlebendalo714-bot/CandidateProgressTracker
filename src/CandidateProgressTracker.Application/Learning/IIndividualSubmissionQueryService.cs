namespace CandidateProgressTracker.Application.Learning;

public record IndividualSubmissionDetails(
    Guid AssignmentId,
    Guid RevisionId,
    string ProjectTitle,
    string MilestoneTitle,
    int RevisionNo,
    DateTimeOffset DueAt,
    string Instructions,
    string AcceptanceExpectations,
    string EvidencePolicy);

public interface IIndividualSubmissionQueryService
{
    Task<SubmissionHistoryPage?> GetMentorHistoryAsync(Guid assignmentId, string mentorUserId, int pageNumber = 1);

    Task<SubmissionHistoryPage?> GetHistoryAsync(Guid assignmentId, string userId, int pageNumber = 1);

    Task<IndividualSubmissionDetails?> GetAsync(
        Guid assignmentId,
        string userId);
}

public record SubmissionHistoryPage(Guid AssignmentId, string ProjectTitle, string MilestoneTitle,
    int PageNumber, bool HasNextPage, IReadOnlyList<SubmissionHistoryAttempt> Attempts)
{
    public string? CandidateName { get; init; }
    public CreditedRoster? Roster { get; init; }
    public string? Eligibility { get; init; }
}

public record SubmissionHistoryAttempt(Guid ReceiptId, int AttemptNo, DateTimeOffset ReceivedAt,
    string Explanation, Guid RevisionId, int RevisionNo, DateTimeOffset DueAt,
    string Instructions, string AcceptanceExpectations, string EvidencePolicy,
    IReadOnlyList<SubmissionHistoryEvidence> Evidence)
{
    public IReadOnlyList<SubmissionFile> Files { get; init; } = Array.Empty<SubmissionFile>();
    public bool IsLate => ReceivedAt > DueAt;
    public string? SubmitterName { get; init; }
    public IReadOnlyList<ReviewHistory> Reviews { get; init; } = Array.Empty<ReviewHistory>();
}

public record SubmissionHistoryEvidence(string Label, string Url)
{
    public bool IsClickable => !Url.Any(char.IsWhiteSpace) && !Url.Any(char.IsControl)
        && Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo);
}
