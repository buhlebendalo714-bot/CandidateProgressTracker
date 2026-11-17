namespace CandidateProgressTracker.Domain.Learning;
public class UploadAttempt
{
    public Guid UploadAttemptId { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid RevisionId { get; set; }
    public string ActorId { get; set; } = "";
    public Guid RequestId { get; set; }
    public string Explanation { get; set; } = "";
    public string LinksJson { get; set; } = "[]";
    public string PayloadHash { get; set; } = "";
    public string RosterConfirmation { get; set; } = "";
    public byte[] ExpectedVersion { get; set; } = [];
    public DateTimeOffset ReceivedAt { get; set; }
    public string Status { get; set; } = "Processing";
    public string Message { get; set; } = "Processing: validation and malware scanning pending.";
    public int Failures { get; set; }
    public DateTimeOffset NextTryAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public Guid? SubmissionId { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool Hold { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public class EvidenceFile
{
    public Guid EvidenceFileId { get; set; } = Guid.NewGuid();
    public Guid UploadAttemptId { get; set; }
    public int SequenceNo { get; set; }
    public string OriginalName { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string MediaType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset? ScannedAt { get; set; }
    public string ScanResult { get; set; } = "Pending";
    public bool Deleted { get; set; }
}
