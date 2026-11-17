using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

internal sealed class TeamTestContext : IAsyncDisposable
{
    private readonly AsyncServiceScope scope;
    internal TrackerDbContext Db { get; }
    internal IServiceProvider Services => scope.ServiceProvider;
    internal IndividualSubmissionTests.Seed Seed { get; private set; } = null!;
    internal Guid TeamId { get; private set; }
    internal string OtherId { get; private set; } = "";
    internal string MentorId { get; private set; } = "";
    internal ITeamSubmissionService Teams => Services.GetRequiredService<ITeamSubmissionService>();
    internal IIndividualSubmissionReviewService Reviews => Services.GetRequiredService<IIndividualSubmissionReviewService>();
    internal IAssignmentReopeningService Reopenings => Services.GetRequiredService<IAssignmentReopeningService>();
    private TeamTestContext(AsyncServiceScope scope) { this.scope = scope; Db = Services.GetRequiredService<TrackerDbContext>(); }
    internal static async Task<TeamTestContext> CreateAsync(WebAccessFixture fixture)
    {
        var t = new TeamTestContext(fixture.Services.CreateAsyncScope());
        try
        {
            t.Seed = await new IndividualSubmissionTests(fixture).SeedAsync(t.Db);
            var project = await t.Db.Projects.SingleAsync(p => p.ProjectId == t.Seed.ProjectId);
            project.Mode = ProjectMode.Team;
            var team = new ProjectTeam { ProjectId = project.ProjectId, Name = "Shared <team>", CreatedBy = t.Seed.AdministratorId, CreatedAt = DateTimeOffset.UtcNow };
            t.Db.ProjectTeams.Add(team); t.TeamId = team.TeamId;
            var assignment = await t.Db.Assignments.SingleAsync(a => a.AssignmentId == t.Seed.AssignmentId);
            assignment.CandidateId = null; assignment.TeamId = team.TeamId;
            await t.Db.SaveChangesAsync();
            var primary = await t.AddUserAsync();
            t.Seed = t.Seed with { CandidateId = primary };
            t.OtherId = await t.AddUserAsync();
            t.MentorId = await t.AddUserAsync("Mentor", false);
            return t;
        }
        catch { await t.DisposeAsync(); throw; }
    }
    internal async Task<string> AddUserAsync(string role = "Candidate", bool team = true)
    {
        var users = Services.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = $"team.{Guid.NewGuid():N}" };
        Assert.True((await users.CreateAsync(user, "Integration-Only!482")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        Db.CohortMemberships.Add(new() { CohortId = Seed.CohortId, UserId = user.Id,
            MembershipKind = role == "Mentor" ? MembershipKind.Mentor : MembershipKind.Candidate,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedBy = Seed.AdministratorId, ChangeReason = "Team workflow test" });
        if (team) AddTeamMembership(user.Id);
        await Db.SaveChangesAsync();
        return user.Id;
    }
    internal void AddTeamMembership(string id) => Db.TeamMemberships.Add(new() { TeamId = TeamId, ProjectId = Seed.ProjectId,
        UserId = id, StartsAt = DateTimeOffset.UtcNow.AddHours(-1), ChangedBy = Seed.AdministratorId, ChangeReason = "Team workflow test" });
    internal async Task<TeamSubmissionRequest> RequestAsync(string? actor = null)
    {
        var form = await Teams.GetFormAsync(Seed.AssignmentId, actor ?? Seed.CandidateId);
        Assert.NotNull(form);
        return new(IndividualSubmissionTests.Request(Seed) with { AssignmentRevisionId = form.Requirements.RevisionId }, form.Confirmation);
    }
    internal async Task<SubmissionReceipt> SubmitAsync(string? actor = null)
    {
        var result = await Teams.SubmitAsync(await RequestAsync(actor), actor ?? Seed.CandidateId);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors)); return result.Receipt!;
    }
    internal async Task<ReviewRequest> DecisionAsync(ReviewDecision decision = ReviewDecision.Approved, string feedback = "")
    {
        var page = (await Reviews.GetReviewAsync(Seed.AssignmentId, MentorId))!;
        return new(Seed.AssignmentId, page.Attempt!.ReceiptId, page.RowVersion, Guid.NewGuid(), decision, feedback);
    }
    internal async Task<ReopeningRequest> ReopeningAsync()
    {
        var page = (await Reopenings.GetReopeningAsync(Seed.AssignmentId, Seed.AdministratorId))!;
        return new(Seed.AssignmentId, page.EffectiveApprovalId ?? Guid.Empty, page.RowVersion, Guid.NewGuid(), "Reconsider the team evidence.");
    }
    internal async Task ReviseAsync(bool requirements = false, bool revert = false)
    {
        var revisions = Services.GetRequiredService<IAssignmentRevisionService>();
        var page = (await revisions.GetAsync(Seed.AssignmentId, Seed.AdministratorId))!;
        var result = await revisions.ReviseAsync(Seed.AssignmentId, page.RowVersion, page.DueAt.AddDays(1),
            revert ? "Complete the work." : requirements ? "Changed team requirements" : page.Instructions,
            page.AcceptanceExpectations, page.EvidencePolicy, "Team revision test", Seed.AdministratorId);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
    }
    internal async Task EndTeamAsync(string id)
    {
        var membership = await Db.TeamMemberships.AsNoTracking().SingleAsync(m => m.ProjectId == Seed.ProjectId && m.UserId == id && m.EndsAt == null);
        var result = await Services.GetRequiredService<IProjectTeamService>().EndAsync(Seed.ProjectId, membership.TeamMembershipId,
            Convert.ToBase64String(membership.RowVersion), "Leaving team", Seed.AdministratorId);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
    }
    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
