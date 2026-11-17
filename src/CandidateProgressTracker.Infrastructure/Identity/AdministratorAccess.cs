using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Identity;

internal static class AdministratorAccess
{
    // Call inside the service transaction so role removal cannot race the operation.
    internal static async Task<bool> IsCurrentAsync(TrackerDbContext db, string actorUserId)
    {
        if(string.IsNullOrWhiteSpace(actorUserId))return false;
        var allowed=await (from userRole in db.UserRoles
         join role in db.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == actorUserId && role.NormalizedName == "ADMINISTRATOR"
             && !db.AccountAccessStates.Any(s => s.UserId == userRole.UserId && !s.Enabled)
         select userRole.UserId).AnyAsync();
        if(allowed)db.AuditActorId=actorUserId;
        return allowed;
    }
}
