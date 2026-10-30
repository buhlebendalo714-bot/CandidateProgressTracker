namespace CandidateProgressTracker.Application.Accounts;

public record AccountSummary(string UserName, string Roles);

public interface IAccountQueryService
{
    Task<IReadOnlyList<AccountSummary>> GetAccountsAsync();
}