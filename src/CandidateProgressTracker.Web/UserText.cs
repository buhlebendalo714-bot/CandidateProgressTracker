using System.Text.RegularExpressions;
namespace CandidateProgressTracker.Web;
// Presentation only: persisted statuses and request values stay unchanged.
public static class UserText
{
    public static string Label(object? value) => value?.ToString() switch
    {
        null or "" => "Not recorded",
        "ChangesRequested" => "Changes requested",
        "EnrolExisting" => "Add existing candidate",
        "Create" => "Create new account",
        "Skip" => "Already enrolled",
        "Conflict" => "Needs attention",
        "Created" => "New account created",
        "Enrolled" => "Existing candidate added",
        "Skipped" => "Already enrolled — unchanged",
        "Pending" => "Waiting to be processed",
        "Failed" => "Could not be completed",
        "Committed" => "Submitted — awaiting review",
        "Processing" => "Checking uploaded files",
        "Rejected" => "Files not accepted",
        "Expired" => "Upload expired",
        "Submitted" => "Awaiting review",
        "IdentityUser" => "User account",
        "AccountCredential" => "Account code",
        "AccountAccessState" => "Account access",
        "OnboardingBatch" => "Candidate import",
        "OnboardingBatchRow" => "Candidate import row",
        "AssignmentReopening" => "Assignment approval",
        var text => Regex.Replace(text!, "(?<=[a-z])(?=[A-Z])", " ")
    };
    public static string AuditAction(string type,string action) => action switch {
        "Added"=>$"{Label(type)} created",
        "Modified"=>$"{Label(type)} updated",
        "Deleted"=>$"{Label(type)} removed",
        _=>Label(action)
    };
    public static string FileSize(long bytes) => bytes >= 1048576 ? $"{bytes / 1048576d:0.#} MB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes} bytes";
}
