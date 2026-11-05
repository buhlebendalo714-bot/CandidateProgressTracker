using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class MemberAssignmentQueryTests
{
    private record Setup(
        string AdminId,
        string CandidateA,
        string CandidateB,
        string MentorId,
        Guid CohortId,
        Guid CycleId,
        Guid IndividualProjectId,
        Guid TeamProjectId,
        Guid TeamId,
        Guid IndividualMilestoneId,
        Guid AssignmentA,
        Guid AssignmentB,
        Guid TeamAssignment);

    private static async Task<string> CreateUserAsync(
        TestDatabase database,
        string role)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser
        {
            UserName = $"visibility.{Guid.NewGuid():N}"
        };

        var created = await users.CreateAsync(
            user, "Integration-Only!482");

        Assert.True(
            created.Succeeded,
            string.Join("; ",
                created.Errors.Select(error => error.Description)));

        var assigned = await users.AddToRoleAsync(user, role);

        Assert.True(
            assigned.Succeeded,
            string.Join("; ",
                assigned.Errors.Select(error => error.Description)));

        return user.Id;
    }

    private static async Task<Setup> ArrangeAsync(TestDatabase database)
    {
        var adminId = await CreateUserAsync(database, "Administrator");
        var candidateA = await CreateUserAsync(database, "Candidate");
        var candidateB = await CreateUserAsync(database, "Candidate");
        var mentorId = await CreateUserAsync(database, "Mentor");

        await using var scope = database.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var now = DateTimeOffset.UtcNow;

        var cohort = new Cohort
        {
            Name = "Visibility cohort",
            Code = $"V-{Guid.NewGuid():N}",
            CreatedAt = now,
            CreatedBy = adminId
        };

        db.Cohorts.Add(cohort);
        await db.SaveChangesAsync();

        foreach (var member in new[]
        {
            (UserId: candidateA, Kind: MembershipKind.Candidate),
            (UserId: candidateB, Kind: MembershipKind.Candidate),
            (UserId: mentorId, Kind: MembershipKind.Mentor)
        })
        {
            db.CohortMemberships.Add(new CohortMembership
            {
                CohortId = cohort.CohortId,
                UserId = member.UserId,
                MembershipKind = member.Kind,
                StartsAt = now.AddDays(-1),
                ChangeReason = "Visibility test setup",
                CreatedBy = adminId
            });
        }

        await db.SaveChangesAsync();

        var cycles = scope.ServiceProvider
            .GetRequiredService<ILearningCycleService>();

        var start = DateOnly.FromDateTime(now.UtcDateTime);

        var cycle = await cycles.CreateAsync(
            cohort.CohortId,
            "Visibility cycle",
            start,
            start.AddMonths(3),
            adminId);

        Assert.True(cycle.Succeeded, string.Join("; ", cycle.Errors));

        var projects = scope.ServiceProvider
            .GetRequiredService<IProjectService>();

        var individual = await projects.CreateAsync(
            cycle.CycleId!.Value,
            "Individual project",
            "Individual brief",
            ProjectMode.Individual,
            adminId);

        Assert.True(
            individual.Succeeded,
            string.Join("; ", individual.Errors));

        var teamProject = await projects.CreateAsync(
            cycle.CycleId.Value,
            "Team project",
            "Team brief",
            ProjectMode.Team,
            adminId);

        Assert.True(
            teamProject.Succeeded,
            string.Join("; ", teamProject.Errors));

        var milestones = scope.ServiceProvider
            .GetRequiredService<IMilestoneService>();

        foreach (var projectId in new[]
        {
            individual.ProjectId!.Value,
            teamProject.ProjectId!.Value
        })
        {
            var result = await milestones.CreateAsync(
                projectId,
                1,
                "Planning",
                "Original instructions",
                "Original expectations",
                "Original evidence",
                adminId);

            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        }

        var individualMilestoneId = await db.Milestones
            .Where(item => item.ProjectId == individual.ProjectId.Value)
            .Select(item => item.MilestoneId)
            .SingleAsync();

        var teamMilestoneId = await db.Milestones
            .Where(item => item.ProjectId == teamProject.ProjectId.Value)
            .Select(item => item.MilestoneId)
            .SingleAsync();

        var teams = scope.ServiceProvider
            .GetRequiredService<IProjectTeamService>();

        var createdTeam = await teams.CreateAsync(
            teamProject.ProjectId.Value, "Alpha", adminId);

        Assert.True(
            createdTeam.Succeeded,
            string.Join("; ", createdTeam.Errors));

        var teamId = await db.ProjectTeams
            .Where(item => item.ProjectId == teamProject.ProjectId.Value)
            .Select(item => item.TeamId)
            .SingleAsync();

        var assignedMember = await teams.AssignAsync(
            teamProject.ProjectId.Value,
            teamId,
            candidateA,
            "Visibility test assignment",
            adminId);

        Assert.True(
            assignedMember.Succeeded,
            string.Join("; ", assignedMember.Errors));

        var assignments = scope.ServiceProvider
            .GetRequiredService<IMilestoneAssignmentService>();

        foreach (var candidateId in new[] { candidateA, candidateB })
        {
            var result = await assignments.CreateAsync(
                individualMilestoneId,
                candidateId,
                null,
                now.AddDays(7),
                true,
                adminId);

            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        }

        var teamAssignment = await assignments.CreateAsync(
            teamMilestoneId,
            null,
            teamId,
            now.AddDays(8),
            true,
            adminId);

        Assert.True(
            teamAssignment.Succeeded,
            string.Join("; ", teamAssignment.Errors));

        var saved = await db.Assignments.AsNoTracking().ToListAsync();

        return new Setup(
            adminId,
            candidateA,
            candidateB,
            mentorId,
            cohort.CohortId,
            cycle.CycleId.Value,
            individual.ProjectId.Value,
            teamProject.ProjectId.Value,
            teamId,
            individualMilestoneId,
            saved.Single(item => item.CandidateId == candidateA).AssignmentId,
            saved.Single(item => item.CandidateId == candidateB).AssignmentId,
            saved.Single(item => item.TeamId == teamId).AssignmentId);
    }

    // Fresh scope for every query, matching separate browser requests.
    private static async Task<MemberAssignmentPage?> QueryAsync(
        TestDatabase database,
        string userId,
        MembershipKind kind,
        int pageNumber = 1)
    {
        await using var scope = database.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IMemberAssignmentQueryService>();

        return await service.GetAsync(userId, kind, pageNumber);
    }

    private static async Task<MemberAssignmentPage> VisibleAsync(
        TestDatabase database,
        string userId,
        MembershipKind kind,
        int pageNumber = 1)
    {
        var result = await QueryAsync(database, userId, kind, pageNumber);
        Assert.NotNull(result);
        return result!;
    }

    [Fact]
    public async Task Candidates_see_only_their_individual_and_current_team_assignments()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        var candidateA = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate);

        Assert.Equal(2, candidateA.Assignments.Count);
        Assert.Contains(candidateA.Assignments,
            item => item.AssignmentId == setup.AssignmentA);
        Assert.Contains(candidateA.Assignments,
            item => item.AssignmentId == setup.TeamAssignment);
        Assert.DoesNotContain(candidateA.Assignments,
            item => item.AssignmentId == setup.AssignmentB);

        var candidateB = await VisibleAsync(
            database, setup.CandidateB, MembershipKind.Candidate);

        Assert.Equal(
            setup.AssignmentB,
            Assert.Single(candidateB.Assignments).AssignmentId);
    }

    [Fact]
    public async Task Ending_team_membership_removes_only_team_assignment()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        // Establish that the team assignment is visible before ending.
        var before = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate);

        Assert.Contains(before.Assignments,
            item => item.AssignmentId == setup.TeamAssignment);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var membership = await db.TeamMemberships.AsNoTracking()
                .SingleAsync(item =>
                    item.TeamId == setup.TeamId &&
                    item.UserId == setup.CandidateA);

            var service = scope.ServiceProvider
                .GetRequiredService<IProjectTeamService>();

            var result = await service.EndAsync(
                setup.TeamProjectId,
                membership.TeamMembershipId,
                Convert.ToBase64String(membership.RowVersion),
                "Visibility test",
                setup.AdminId);

            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        }

        var after = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate);

        Assert.Equal(
            setup.AssignmentA,
            Assert.Single(after.Assignments).AssignmentId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Noncurrent_cohort_membership_hides_all_candidate_assignments(
        bool futureMembership)
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var membership = await db.CohortMemberships.SingleAsync(item =>
                item.CohortId == setup.CohortId &&
                item.UserId == setup.CandidateA &&
                item.MembershipKind == MembershipKind.Candidate);

            if (futureMembership)
            {
                membership.StartsAt = DateTimeOffset.UtcNow.AddDays(1);
            }
            else
            {
                membership.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            }

            await db.SaveChangesAsync();
        }

        var result = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate);

        Assert.Empty(result.Assignments);
    }

    [Fact]
    public async Task Mentor_visibility_requires_current_membership_in_each_cohort()
    {
        await using var database = await TestDatabase.CreateAsync();
        var first = await ArrangeAsync(database);
        var other = await ArrangeAsync(database);

        var before = await VisibleAsync(
            database, first.MentorId, MembershipKind.Mentor);

        Assert.Equal(3, before.Assignments.Count);
        Assert.Contains(before.Assignments,
            item => item.AssignmentId == first.AssignmentA);
        Assert.Contains(before.Assignments,
            item => item.AssignmentId == first.AssignmentB);
        Assert.Contains(before.Assignments,
            item => item.AssignmentId == first.TeamAssignment);

        Assert.DoesNotContain(before.Assignments,
            item => item.AssignmentId == other.AssignmentA);
        Assert.DoesNotContain(before.Assignments,
            item => item.AssignmentId == other.TeamAssignment);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var membership = await db.CohortMemberships.SingleAsync(item =>
                item.UserId == first.MentorId &&
                item.CohortId == first.CohortId);

            membership.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var after = await VisibleAsync(
            database, first.MentorId, MembershipKind.Mentor);

        Assert.Empty(after.Assignments);
    }

    [Fact]
    public async Task Wrong_role_unknown_user_and_invalid_inputs_are_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        Assert.Null(await QueryAsync(
            database, setup.CandidateA, MembershipKind.Mentor));

        Assert.Null(await QueryAsync(
            database, setup.MentorId, MembershipKind.Candidate));

        // Administrator alone does not imply either member role.
        Assert.Null(await QueryAsync(
            database, setup.AdminId, MembershipKind.Candidate));

        Assert.Null(await QueryAsync(
            database, setup.AdminId, MembershipKind.Mentor));

        Assert.Null(await QueryAsync(
            database, "", MembershipKind.Candidate));

        Assert.Null(await QueryAsync(
            database, Guid.NewGuid().ToString(), MembershipKind.Candidate));

        Assert.Null(await QueryAsync(
            database, setup.CandidateA, (MembershipKind)99));

        Assert.Null(await QueryAsync(
            database, setup.CandidateA, MembershipKind.Candidate, 0));

        Assert.Null(await QueryAsync(
            database, setup.CandidateA, MembershipKind.Candidate, 100001));
    }

    [Fact]
    public async Task Removing_candidate_role_revokes_access_despite_existing_memberships()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        var before = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate);

        Assert.Equal(2, before.Assignments.Count);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            var user = await users.FindByIdAsync(setup.CandidateA);
            Assert.NotNull(user);

            var removed = await users.RemoveFromRoleAsync(user!, "Candidate");

            Assert.True(
                removed.Succeeded,
                string.Join("; ",
                    removed.Errors.Select(error => error.Description)));
        }

        Assert.Null(await QueryAsync(
            database, setup.CandidateA, MembershipKind.Candidate));
    }

    [Theory]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("cohort")]
    public async Task Archived_ancestors_hide_assignments_from_candidates_and_mentors(
        string level)
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var now = DateTimeOffset.UtcNow;

            if (level == "project")
            {
                var project = await db.Projects.SingleAsync(item =>
                    item.ProjectId == setup.IndividualProjectId);

                project.IsArchived = true;
                project.ArchivedAt = now;
                project.ArchivedBy = setup.AdminId;
                project.ArchiveReason = "Visibility test";
            }
            else if (level == "cycle")
            {
                var cycle = await db.LearningCycles.SingleAsync(item =>
                    item.CycleId == setup.CycleId);

                cycle.IsArchived = true;
                cycle.ArchivedAt = now;
                cycle.ArchivedBy = setup.AdminId;
                cycle.ArchiveReason = "Visibility test";
            }
            else
            {
                var cohort = await db.Cohorts.SingleAsync(item =>
                    item.CohortId == setup.CohortId);

                cohort.IsArchived = true;
                cohort.ArchivedAt = now;
                cohort.ArchivedBy = setup.AdminId;
                cohort.ArchiveReason = "Visibility test";
            }

            await db.SaveChangesAsync();
        }

        var candidate = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate);

        var mentor = await VisibleAsync(
            database, setup.MentorId, MembershipKind.Mentor);

        if (level == "project")
        {
            // The separate, active Team project remains visible.
            Assert.Equal(
                setup.TeamAssignment,
                Assert.Single(candidate.Assignments).AssignmentId);

            Assert.Equal(
                setup.TeamAssignment,
                Assert.Single(mentor.Assignments).AssignmentId);
        }
        else
        {
            Assert.Empty(candidate.Assignments);
            Assert.Empty(mentor.Assignments);
        }
    }

    [Fact]
    public async Task Latest_saved_revision_supplies_requirements_and_deadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);
        var revisedDueAt = DateTimeOffset.UtcNow.AddDays(12);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            // Arrange a revision directly because revision editing is not built yet.
            db.AssignmentRevisions.Add(new AssignmentRevision
            {
                AssignmentId = setup.AssignmentA,
                RevisionNo = 2,
                DueAt = revisedDueAt,
                InstructionsSnapshot = "Revision two instructions",
                AcceptanceSnapshot = "Revision two expectations",
                EvidencePolicySnapshot = "Revision two evidence",
                ChangedAt = DateTimeOffset.UtcNow,
                ChangedBy = setup.AdminId,
                Reason = "Revision visibility test"
            });

            var assignment = await db.Assignments.SingleAsync(item =>
                item.AssignmentId == setup.AssignmentA);

            assignment.DueAt = revisedDueAt;

            var milestone = await db.Milestones.SingleAsync(item =>
                item.MilestoneId == setup.IndividualMilestoneId);

            milestone.Instructions = "Different live instructions";
            milestone.AcceptanceExpectations = "Different live expectations";
            milestone.EvidencePolicy = "Different live evidence";

            await db.SaveChangesAsync();
        }

        foreach (var viewer in new[]
        {
            (UserId: setup.CandidateA, Kind: MembershipKind.Candidate),
            (UserId: setup.MentorId, Kind: MembershipKind.Mentor)
        })
        {
            var result = await VisibleAsync(
                database, viewer.UserId, viewer.Kind);

            var assignment = Assert.Single(
                result.Assignments,
                item => item.AssignmentId == setup.AssignmentA);

            Assert.Equal(2, assignment.RevisionNo);
            Assert.Equal(revisedDueAt, assignment.DueAt);
            Assert.Equal("Revision two instructions", assignment.Instructions);
            Assert.Equal(
                "Revision two expectations",
                assignment.AcceptanceExpectations);
            Assert.Equal("Revision two evidence", assignment.EvidencePolicy);
        }

        // Candidate B retains their original revision for the same milestone.
        var candidateB = await VisibleAsync(
            database, setup.CandidateB, MembershipKind.Candidate);

        var original = Assert.Single(candidateB.Assignments);

        Assert.Equal(1, original.RevisionNo);
        Assert.Equal("Original instructions", original.Instructions);
        Assert.Equal("Original expectations", original.AcceptanceExpectations);
        Assert.Equal("Original evidence", original.EvidencePolicy);
    }

    [Fact]
    public async Task Pagination_is_bounded_ordered_and_does_not_duplicate_assignments()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await ArrangeAsync(database);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var milestones = scope.ServiceProvider
                .GetRequiredService<IMilestoneService>();

            var assignments = scope.ServiceProvider
                .GetRequiredService<IMilestoneAssignmentService>();

            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            // Equal deadlines exercise the AssignmentId tie-breaker.
            var dueAt = DateTimeOffset.UtcNow.AddDays(10);

            for (var sequence = 2; sequence <= 27; sequence++)
            {
                var created = await milestones.CreateAsync(
                    setup.IndividualProjectId,
                    sequence,
                    $"Milestone {sequence}",
                    "Instructions",
                    "Expectations",
                    "Evidence",
                    setup.AdminId);

                Assert.True(
                    created.Succeeded,
                    string.Join("; ", created.Errors));

                var milestoneId = await db.Milestones
                    .Where(item =>
                        item.ProjectId == setup.IndividualProjectId &&
                        item.SequenceNo == sequence)
                    .Select(item => item.MilestoneId)
                    .SingleAsync();

                var assigned = await assignments.CreateAsync(
                    milestoneId,
                    setup.CandidateA,
                    null,
                    dueAt,
                    true,
                    setup.AdminId);

                Assert.True(
                    assigned.Succeeded,
                    string.Join("; ", assigned.Errors));
            }
        }

        var first = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate, 1);

        var second = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate, 2);

        var repeatedFirst = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate, 1);

        Assert.Equal(25, first.Assignments.Count);
        Assert.Equal(3, second.Assignments.Count);
        Assert.True(first.HasNextPage);
        Assert.False(second.HasNextPage);
        Assert.Equal(1, first.PageNumber);
        Assert.Equal(2, second.PageNumber);

        var firstIds = first.Assignments
            .Select(item => item.AssignmentId)
            .ToArray();

        var secondIds = second.Assignments
            .Select(item => item.AssignmentId)
            .ToArray();

        Assert.Empty(firstIds.Intersect(secondIds));
        Assert.Equal(28, firstIds.Concat(secondIds).Distinct().Count());

        Assert.Equal(
            firstIds,
            repeatedFirst.Assignments
                .Select(item => item.AssignmentId)
                .ToArray());

        var combined = first.Assignments
            .Concat(second.Assignments)
            .ToArray();

        Assert.True(combined.Zip(
            combined.Skip(1),
            (left, right) => left.DueAt <= right.DueAt).All(value => value));

        var third = await VisibleAsync(
            database, setup.CandidateA, MembershipKind.Candidate, 3);

        Assert.Empty(third.Assignments);
        Assert.False(third.HasNextPage);
    }
}