namespace CandidateProgressTracker.Application.Cohorts;

public enum OnboardingAction { Create, EnrolExisting, Skip, Conflict }
public sealed record OnboardingIssue(int Row, string Field, string Message);
public sealed record OnboardingPreviewRow(int Row, string UserName, string Email,
    string? MatchedUserName, string? MatchedEmail, OnboardingAction Action,
    string Explanation, IReadOnlyList<OnboardingIssue> Issues, string? MatchedUserId = null);
public sealed record BulkOnboardingPreview(Guid CohortId, string CohortName, DateOnly StartDate,
    IReadOnlyList<OnboardingPreviewRow> Rows, IReadOnlyList<OnboardingIssue> Issues);
public interface IBulkOnboardingPreviewService
{
    const int MaximumCandidates = 100;
    const int MaximumBytes = 1_048_576;
    Task<BulkOnboardingPreview?> PreviewAsync(Guid cohortId, DateOnly startDate, byte[] csv, string actor);
}
