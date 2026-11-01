using System.Data;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Learning;

public class ProjectService : IProjectService
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public ProjectService(
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
            await _users.IsInRoleAsync(user, "Administrator");
    }

    public async Task<CycleProjects?> GetForCycleAsync(
        Guid cycleId,
        string actorUserId)
    {
        if (!await IsAdministratorAsync(actorUserId))
        {
            return null;
        }

        var parent = await (
            from cycle in _db.LearningCycles.AsNoTracking()
            join cohort in _db.Cohorts.AsNoTracking()
                on cycle.CohortId equals cohort.CohortId
            where cycle.CycleId == cycleId
            select new
            {
                cycle.CycleId,
                cohort.CohortId,
                CycleName = cycle.Name,
                CohortName = cohort.Name,
                IsReadOnly = cycle.IsArchived || cohort.IsArchived
            })
            .SingleOrDefaultAsync();

        if (parent is null)
        {
            return null;
        }

        var projects = await _db.Projects
            .AsNoTracking()
            .Where(project => project.CycleId == cycleId)
            .OrderBy(project => project.Title)
            .ThenBy(project => project.ProjectId)
            .Select(project => new ProjectSummary(
                project.ProjectId,
                project.Title,
                project.Brief,
                project.Mode,
                project.IsArchived))
            .ToListAsync();

        return new CycleProjects(
            parent.CycleId,
            parent.CohortId,
            parent.CycleName,
            parent.CohortName,
            parent.IsReadOnly,
            projects);
    }

    public async Task<ProjectCreationResult> CreateAsync(
        Guid cycleId,
        string title,
        string brief,
        ProjectMode mode,
        string actorUserId)
    {
        static ProjectCreationResult Failure(string message) =>
            new(false, null, new[] { message });

        var trimmedTitle = title?.Trim() ?? string.Empty;
        var trimmedBrief = brief?.Trim() ?? string.Empty;

        if (trimmedTitle.Length is < 1 or > 200)
        {
            return Failure("Enter a project title of up to 200 characters.");
        }

        if (trimmedBrief.Length is < 1 or > 10000)
        {
            return Failure("Enter a project brief of up to 10,000 characters.");
        }

        if (mode != ProjectMode.Individual && mode != ProjectMode.Team)
        {
            return Failure("Select Individual or Team mode.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        if (!await IsAdministratorAsync(actorUserId))
        {
            return Failure("Administrator access is required.");
        }

        var parent = await (
            from cycle in _db.LearningCycles
            join cohort in _db.Cohorts
                on cycle.CohortId equals cohort.CohortId
            where cycle.CycleId == cycleId
            select new
            {
                CycleArchived = cycle.IsArchived,
                CohortArchived = cohort.IsArchived
            })
            .SingleOrDefaultAsync();

        if (parent is null)
        {
            return Failure("The selected learning cycle was not found.");
        }

        if (parent.CycleArchived || parent.CohortArchived)
        {
            return Failure(
                "You cannot create a project in an archived cycle or cohort.");
        }

        var project = new Project
        {
            CycleId = cycleId,
            Title = trimmedTitle,
            Brief = trimmedBrief,
            Mode = mode,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorUserId
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new ProjectCreationResult(
            true, project.ProjectId, Array.Empty<string>());
    }
}