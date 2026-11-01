namespace CandidateProgressTracker.Domain.Learning;

public class ProjectTeam
{
    public Guid TeamId { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}