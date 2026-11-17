namespace CandidateProgressTracker.Domain.Learning;
public class Course
{
    public Guid CourseId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Url { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public class CourseOffering
{
    public Guid OfferingId { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid CycleId { get; set; }
    public string TitleSnapshot { get; set; } = "";
    public string PlatformSnapshot { get; set; } = "";
    public string UrlSnapshot { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
