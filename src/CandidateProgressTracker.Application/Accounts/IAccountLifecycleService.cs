namespace CandidateProgressTracker.Application.Accounts;

public sealed record ManagedAccount(string UserId, string UserName, string? Email, string Roles, bool Enabled, string Version);
public sealed record CredentialReceipt(string UserName, string Credential, DateTimeOffset ExpiresAt, string Kind = "Activation");
public sealed record AccountOperation(bool Succeeded, string? Error = null, CredentialReceipt? Credential = null);
public interface IAccountLifecycleService
{
    Task<ManagedAccount?> GetAsync(string userId, string actor);
    Task<AccountOperation> CreateAsync(string userName, string email, string role, string actor);
    Task<AccountOperation> UpdateAsync(string userId, string email, string[] roles, string version, string reason, string actor);
    Task<AccountOperation> DeactivateAsync(string userId, string version, string reason, string actor);
    Task<AccountOperation> IssueAsync(string userId, string kind, string version, string reason, string actor);
    Task<AccountOperation> RedeemAsync(string userName, string credential, string password);
}
