namespace CandidateProgressTracker.Application.Learning;
public static class FileLimits
{
    public const int Count = 5;
    public const long PerFile = 10485760;
    public const long Total = 52428800;
}
public record EvidenceUpload(string Name, Stream Content);
public record FileSubmissionRequest(IndividualSubmissionRequest Content, string RosterConfirmation, IReadOnlyList<EvidenceUpload> Files);
public record ProcessingReceipt(Guid AttemptId, string Status, string Message, Guid? SubmissionId);
public record EvidenceDownload(Stream Content, string Name, string MediaType);
public record SubmissionFile(Guid FileId, string Name, long SizeBytes);
public interface IFileEvidenceService
{
    Task<ProcessingReceipt> ReceiveAsync(FileSubmissionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<ProcessingReceipt?> GetAsync(Guid attemptId, string actor);
    Task<EvidenceDownload?> DownloadAsync(Guid fileId, string actor, CancellationToken cancellationToken = default);
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
    Task CleanupAsync(CancellationToken cancellationToken = default);
}
public record StoredEvidence(string Key, string Name, string MediaType, long Length, string Hash);
public interface IPrivateEvidenceStorage
{
    Task<StoredEvidence> StoreAsync(string name, Stream input, CancellationToken cancellationToken);
    Task<Stream> OpenVerifiedAsync(string key, long length, string hash, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
    IEnumerable<(string Key, DateTimeOffset Created)> OwnedObjects();
}
public enum MalwareResult { Clean, Unsafe, Unavailable }
public interface IMalwareScanner
{
    Task<MalwareResult> ScanAsync(Stream content, CancellationToken cancellationToken);
}
