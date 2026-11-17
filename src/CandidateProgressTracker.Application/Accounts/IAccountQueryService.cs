namespace CandidateProgressTracker.Application.Accounts;

public record AccountSummary(string UserName, string Roles, string UserId = "");

public interface IAccountQueryService
{
    Task<IReadOnlyList<AccountSummary>?> GetAccountsAsync(string actorUserId);
}
