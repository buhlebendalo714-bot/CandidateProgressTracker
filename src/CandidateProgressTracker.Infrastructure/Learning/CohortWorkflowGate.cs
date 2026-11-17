using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

internal static class CohortWorkflowGate
{
    // Routing reads are deliberately nonlocking and never authorize an action.
    // The caller must reread/authorize under the gate in its serializable transaction.
    internal static async Task ForAssignmentAsync(TrackerDbContext db, Guid id) => await LockAsync(db,
        await db.Database.SqlQuery<Guid>($"SELECT c.[CohortId] AS [Value] FROM [Assignments] a WITH (NOLOCK) JOIN [LearningCycles] c WITH (NOLOCK) ON c.[CycleId]=a.[CycleId] WHERE a.[AssignmentId]={id}").SingleOrDefaultAsync());
    internal static async Task ForProjectAsync(TrackerDbContext db, Guid id) => await LockAsync(db,
        await db.Database.SqlQuery<Guid>($"SELECT c.[CohortId] AS [Value] FROM [Projects] p WITH (NOLOCK) JOIN [LearningCycles] c WITH (NOLOCK) ON c.[CycleId]=p.[CycleId] WHERE p.[ProjectId]={id}").SingleOrDefaultAsync());
    internal static async Task ForMembershipAsync(TrackerDbContext db, Guid id) => await LockAsync(db,
        await db.Database.SqlQuery<Guid>($"SELECT [CohortId] AS [Value] FROM [CohortMemberships] WITH (NOLOCK) WHERE [MembershipId]={id}").SingleOrDefaultAsync());
    internal static Task LockAsync(TrackerDbContext db, Guid cohortId)
    {
        var resource = $"CandidateProgressTracker:Cohort:{cohortId:N}";
        return db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51010, 'Workflow is busy. Retry the same request.', 1;");
    }
}
