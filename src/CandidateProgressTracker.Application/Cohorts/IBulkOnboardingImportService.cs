using System.Security.Cryptography;
using System.Text.Json;
namespace CandidateProgressTracker.Application.Cohorts;

public sealed record OnboardingConfirmation(Guid RequestId, Guid CohortId, DateOnly StartDate,
    byte[] Csv, string Fingerprint, string Actor);
public sealed record OnboardingImportRow(int Row, string UserName, string Status, string? Message, string? UserId = null);
public sealed record OnboardingImportResult(Guid BatchId, bool Complete, string? Message, IReadOnlyList<OnboardingImportRow> Rows);
public sealed record OnboardingSavedBatch(Guid BatchId, DateTimeOffset CreatedAt);
public interface IBulkOnboardingImportService
{
    Task<IReadOnlyList<OnboardingSavedBatch>> ListAsync(Guid cohortId, string actor);
    Task<bool> IsAvailableAsync(string actor);
    Task<OnboardingImportResult?> ConfirmAsync(OnboardingConfirmation request, string actor);
    Task<OnboardingImportResult?> ResumeAsync(Guid batchId, string actor);
    Task<OnboardingImportResult?> GetAsync(Guid batchId, string actor);
}
public static class OnboardingFingerprint
{
    public static string Of(BulkOnboardingPreview preview) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(preview)));
}
