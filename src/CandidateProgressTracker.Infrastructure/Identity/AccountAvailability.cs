using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Identity;

public static class AccountAvailability
{
    public static Task<bool> IsActiveAsync(TrackerDbContext db, string userId) =>
        db.Users.AnyAsync(u => u.Id == userId && !db.AccountAccessStates.Any(s => s.UserId == userId && !s.Enabled));
}
