using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class MilestoneService : IMilestoneService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public MilestoneService(
        TrackerDbContext db,
        UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<bool> IsAdministratorAsync(string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return false;
        }

        var user = await _users.FindByIdAsync(actorUserId);

        return user is not null &&
            (await CandidateProgressTracker.Infrastructure.Identity.AccountAvailability.IsActiveAsync(_db, user.Id) && await _users.IsInRoleAsync(user, "Administrator"));
    }

    public async Task<ProjectMilestones?> GetForProjectAsync(
        Guid projectId,
        string actorUserId)
    {
        if (!await IsAdministratorAsync(actorUserId))
        {
            return null;
        }

        var parent = await (
            from project in _db.Projects.AsNoTracking()
            join cycle in _db.LearningCycles.AsNoTracking()
                on project.CycleId equals cycle.CycleId
            join cohort in _db.Cohorts.AsNoTracking()
                on cycle.CohortId equals cohort.CohortId
            where project.ProjectId == projectId
            select new
            {
                project.ProjectId,
                project.CycleId,
                project.Title,
                IsReadOnly = project.IsArchived ||
                    cycle.IsArchived ||
                    cohort.IsArchived
            })
            .SingleOrDefaultAsync();

        if (parent is null)
        {
            return null;
        }

        var milestones = await _db.Milestones
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.SequenceNo)
            .Select(item => new MilestoneSummary(
                item.MilestoneId,
                item.SequenceNo,
                item.Title,
                item.Instructions,
                item.AcceptanceExpectations,
                item.EvidencePolicy))
            .ToListAsync();

        return new ProjectMilestones(
            parent.ProjectId,
            parent.CycleId,
            parent.Title,
            parent.IsReadOnly,
            milestones);
    }

    public async Task<MilestoneCreationResult> CreateAsync(
        Guid projectId,
        int sequenceNo,
        string title,
        string instructions,
        string acceptanceExpectations,
        string evidencePolicy,
        string actorUserId)
    {
        static MilestoneCreationResult Failure(string message) =>
            new(false, null, new[] { message });

        var cleanTitle = title?.Trim() ?? string.Empty;
        var cleanInstructions = instructions?.Trim() ?? string.Empty;
        var cleanAcceptance = acceptanceExpectations?.Trim() ?? string.Empty;
        var cleanEvidence = evidencePolicy?.Trim() ?? string.Empty;

        if (sequenceNo < 1)
        {
            return Failure("Enter a sequence number greater than zero.");
        }

        if (cleanTitle.Length is < 1 or > 200)
        {
            return Failure("Enter a title of up to 200 characters.");
        }

        if (cleanInstructions.Length is < 1 or > 10000)
        {
            return Failure("Enter instructions of up to 10,000 characters.");
        }

        if (cleanAcceptance.Length is < 1 or > 10000)
        {
            return Failure(
                "Enter acceptance expectations of up to 10,000 characters.");
        }

        if (cleanEvidence.Length is < 1 or > 2000)
        {
            return Failure(
                "Enter evidence requirements of up to 2,000 characters.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var parent = await (
            from project in _db.Projects
            join cycle in _db.LearningCycles
                on project.CycleId equals cycle.CycleId
            join cohort in _db.Cohorts
                on cycle.CohortId equals cohort.CohortId
            where project.ProjectId == projectId
            select new
            {
                IsReadOnly = project.IsArchived ||
                    cycle.IsArchived ||
                    cohort.IsArchived
            })
            .SingleOrDefaultAsync();

        if (parent is null)
        {
            return Failure("The selected project was not found.");
        }

        if (parent.IsReadOnly)
        {
            return Failure(
                "You cannot create a milestone in an archived project, cycle, or cohort.");
        }

        var duplicate = await _db.Milestones.AnyAsync(item =>
            item.ProjectId == projectId &&
            item.SequenceNo == sequenceNo);

        if (duplicate)
        {
            return Failure(
                "This sequence number is already used in this project.");
        }

        var milestone = new Milestone
        {
            ProjectId = projectId,
            SequenceNo = sequenceNo,
            Title = cleanTitle,
            Instructions = cleanInstructions,
            AcceptanceExpectations = cleanAcceptance,
            EvidencePolicy = cleanEvidence,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorUserId
        };

        _db.Milestones.Add(milestone);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new MilestoneCreationResult(
                true, milestone.MilestoneId, Array.Empty<string>());
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627) &&
                  sql.Message.Contains("IX_Milestones_ProjectId_SequenceNo"))
        {
            await transaction.RollbackAsync();
            _db.Entry(milestone).State = EntityState.Detached;

            return Failure(
                "This sequence number is already used in this project.");
        }
    }
}