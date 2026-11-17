using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

internal static class TeamSubmissionAccess
{
    internal sealed record Target(Assignment Assignment, Guid CohortId, string TeamName, string ProjectTitle, string MilestoneTitle);
    internal static async Task<Target?> TargetAsync(TrackerDbContext db, Guid id) => await (
        from a in db.Assignments.AsNoTracking()
        join p in db.Projects on a.ProjectId equals p.ProjectId
        join c in db.LearningCycles on p.CycleId equals c.CycleId
        join cohort in db.Cohorts on c.CohortId equals cohort.CohortId
        join t in db.ProjectTeams on new { TeamId = a.TeamId, a.ProjectId } equals new { TeamId = (Guid?)t.TeamId, ProjectId = (Guid?)t.ProjectId }
        join m in db.Milestones on a.MilestoneId equals m.MilestoneId
        where a.AssignmentId == id && a.CandidateId == null && a.TeamId != null && p.Mode == ProjectMode.Team
            && !p.IsArchived && !c.IsArchived && !cohort.IsArchived
        select new Target(a, cohort.CohortId, t.Name, p.Title, m.Title)).SingleOrDefaultAsync();

    internal static async Task<bool> CandidateAsync(TrackerDbContext db, Target target, string actor, DateTimeOffset now)
    {
        return await AssignmentAccess.RoleAsync(db,actor,"CANDIDATE")
            && await db.CohortMemberships.AnyAsync(m => m.CohortId == target.CohortId && m.UserId == actor
                && m.MembershipKind == MembershipKind.Candidate && m.StartsAt <= now && (m.EndsAt == null || m.EndsAt > now));
    }
    internal static Task<bool> MemberAsync(TrackerDbContext db, Target t, string actor, DateTimeOffset now) =>
        db.TeamMemberships.AnyAsync(m => m.TeamId == t.Assignment.TeamId && m.ProjectId == t.Assignment.ProjectId
            && m.UserId == actor && m.StartsAt <= now && (m.EndsAt == null || m.EndsAt > now));

    internal static async Task<List<CreditedMember>> EligibleAsync(TrackerDbContext db, Target t, DateTimeOffset now)
    {
        var roles = from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                    where r.NormalizedName == "CANDIDATE" && !db.AccountAccessStates.Any(s => s.UserId == ur.UserId && !s.Enabled) select ur.UserId;
        return await (from tm in db.TeamMemberships
                      join cm in db.CohortMemberships on tm.UserId equals cm.UserId
                      join u in db.Users on tm.UserId equals u.Id
                      where tm.TeamId == t.Assignment.TeamId && tm.ProjectId == t.Assignment.ProjectId
                          && tm.StartsAt <= now && (tm.EndsAt == null || tm.EndsAt > now)
                          && cm.CohortId == t.CohortId && cm.MembershipKind == MembershipKind.Candidate
                          && cm.StartsAt <= now && (cm.EndsAt == null || cm.EndsAt > now) && roles.Contains(u.Id)
                      orderby u.Id
                      select new CreditedMember(u.Id, u.UserName ?? "(No username)", tm.TeamMembershipId, cm.MembershipId)).ToListAsync();
    }

    internal static async Task<CreditedRoster?> RosterAsync(TrackerDbContext db, Guid id)
    {
        var roster = await db.AssignmentCreditedRosters.AsNoTracking().SingleOrDefaultAsync(r => r.AssignmentId == id);
        if (roster is null) return null;
        var members = await db.AssignmentCreditedRosterMembers.AsNoTracking().Where(m => m.AssignmentId == id)
            .OrderBy(m => m.UserId).Select(m => new CreditedMember(m.UserId, m.UserNameSnapshot, m.TeamMembershipId, m.CohortMembershipId)).ToListAsync();
        return new(roster.TeamNameSnapshot, roster.FrozenAt, roster.FrozenBy, members);
    }

    internal static string Fingerprint(Guid id, IReadOnlyList<CreditedMember> members) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { AssignmentId = id, Members = members }))));

    internal static string Eligibility(CreditedRoster? roster, List<CreditedMember> eligible, string actor, bool member, bool approved)
    {
        if (roster is not null && !eligible.Any(e => roster.Members.Any(m => m.UserId == e.UserId)))
            return "None of the recorded members can currently submit. Ask your Administrator about a new team and assignment for replacement work.";
        if (!member) return "You need current membership in this team to open its work. Previously recorded credit does not restore access.";
        if (roster is not null && !roster.Members.Any(m => m.UserId == actor))
            return "You can view this work, but cannot submit: you joined after its included members were recorded.";
        if (approved) return "This assignment is approved. New submissions are blocked until an Administrator reopens the approval or the requirements change.";
        return "You can submit evidence for this team assignment.";
    }
}
