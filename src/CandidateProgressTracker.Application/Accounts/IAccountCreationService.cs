namespace CandidateProgressTracker.Application.Accounts;

public record AccountCreationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors);

public interface IAccountCreationService
{
    Task<AccountCreationResult> CreateAsync(
        string userName,
        string password,
        string role);
}