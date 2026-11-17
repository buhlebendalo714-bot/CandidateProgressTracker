using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Learning;
internal static class AssignmentAccess
{
    internal sealed record Target(Assignment Assignment, Guid CohortId, string Group, string Title);
    internal static async Task<Target?> TargetAsync(TrackerDbContext db, Guid id, bool allowArchived = false)
    {
        var a = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == id);
        if (a is null) return null;
        var parent = await (from c in db.LearningCycles join h in db.Cohorts on c.CohortId equals h.CohortId
            where c.CycleId == a.CycleId && (allowArchived || (!c.IsArchived && !h.IsArchived)) select c.CohortId).SingleOrDefaultAsync();
        if (parent == Guid.Empty) return null;
        if (a.OfferingId is not null)
        {
            var o = await db.CourseOfferings.AsNoTracking().SingleOrDefaultAsync(o => o.OfferingId == a.OfferingId && o.CycleId == a.CycleId);
            return o is null || a.TeamId is not null ? null : new(a, parent, o.PlatformSnapshot, o.TitleSnapshot);
        }
        return await (from p in db.Projects join m in db.Milestones on p.ProjectId equals m.ProjectId
            where p.ProjectId == a.ProjectId && p.CycleId == a.CycleId && m.MilestoneId == a.MilestoneId && (allowArchived || !p.IsArchived)
                && ((a.CandidateId != null && a.TeamId == null && p.Mode == ProjectMode.Individual) || (a.CandidateId == null && a.TeamId != null && p.Mode == ProjectMode.Team))
            select new Target(a, parent, p.Title, m.Title)).SingleOrDefaultAsync();
    }
    internal static async Task<bool> RoleAsync(TrackerDbContext db, string actor, string role)
    {
        var allowed=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == actor && r.NormalizedName == role && !db.AccountAccessStates.Any(s => s.UserId == ur.UserId && !s.Enabled) select ur.UserId).AnyAsync();
        if(allowed)db.AuditActorId=actor;
        return allowed;
    }
    internal static Task<bool> MembershipAsync(TrackerDbContext db, Guid cohort, string actor, MembershipKind kind, DateTimeOffset now) =>
        db.CohortMemberships.AnyAsync(m => m.CohortId == cohort && m.UserId == actor && m.MembershipKind == kind && m.StartsAt <= now && (m.EndsAt == null || m.EndsAt > now));
    internal static async Task<bool> CandidateAsync(TrackerDbContext db, Target t, string actor, bool submit)
    {
        if (!await RoleAsync(db, actor, "CANDIDATE") || !await MembershipAsync(db, t.CohortId, actor, MembershipKind.Candidate, DateTimeOffset.UtcNow)) return false;
        if (t.Assignment.TeamId is null) return t.Assignment.CandidateId == actor;
        if (!await db.TeamMemberships.AnyAsync(m => m.TeamId == t.Assignment.TeamId && m.ProjectId == t.Assignment.ProjectId && m.UserId == actor && m.StartsAt <= DateTimeOffset.UtcNow && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow))) return false;
        return !submit || !await db.AssignmentCreditedRosters.AnyAsync(r => r.AssignmentId == t.Assignment.AssignmentId)
            || await db.AssignmentCreditedRosterMembers.AnyAsync(m => m.AssignmentId == t.Assignment.AssignmentId && m.UserId == actor);
    }
    internal static async Task<bool> StaffAsync(TrackerDbContext db, Target t, string actor, string role) =>
        await RoleAsync(db, actor, role) && (role == "ADMINISTRATOR" || await MembershipAsync(db, t.CohortId, actor, MembershipKind.Mentor, DateTimeOffset.UtcNow));
}
