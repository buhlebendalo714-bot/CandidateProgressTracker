using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Cohorts;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private int _disposed;
    internal string AccountAdministratorId { get; private set; } = "";
    public ServiceProvider Services { get; }

    private TestDatabase(ServiceProvider services)
    {
        Services = services;
    }

    public static async Task<TestDatabase> CreateAsync(string? targetMigration = null)
    {
        var databaseName =
            $"CandidateProgressTracker_Tests_{Guid.NewGuid():N}";

        var connectionString =
            $@"Server=.\SQLEXPRESS;Database={databaseName};" +
            "Trusted_Connection=True;Encrypt=True;" +
            "TrustServerCertificate=True;";

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(new AuditCaptureOptions { Enabled = targetMigration is null });

        services.AddDbContext<TrackerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TrackerDbContext>();

        services.AddScoped<
            IAccountCreationService,
            AccountCreationService>();
        services.AddScoped<IAccountQueryService, AccountQueryService>();
        services.AddScoped<ICohortMembershipQueryService, CohortMembershipQueryService>();

        services.AddScoped<
            ICohortCreationService, 
            CohortCreationService>();

        services.AddScoped<
            ICohortMembershipService, 
            CohortMembershipService>();

        services.AddScoped<
            ICohortMembershipEndingService,
            CohortMembershipEndingService>();

        services.AddScoped<
            ICohortArchiveService, 
            CohortArchiveService>();

        services.AddScoped<
            ICohortQueryService, 
            CohortQueryService>();

        services.AddScoped<
            IMentorCohortQueryService, 
            MentorCohortQueryService>();

        services.AddScoped<
            ILearningCycleService, 
            LearningCycleService>();

        services.AddScoped<
            IProjectService, 
            ProjectService>();

        services.AddScoped<
            IMilestoneService, 
            MilestoneService>();
            
        services.AddScoped<
            IMemberCycleQueryService, 
            MemberCycleQueryService>();

        services.AddScoped<
            IProjectTeamService, 
            ProjectTeamService>();

        services.AddScoped<
            IMilestoneAssignmentService, 
            MilestoneAssignmentService>();

        services.AddScoped<
            IMemberAssignmentQueryService,
            MemberAssignmentQueryService>();

        services.AddScoped<
            IAssignmentRevisionService,
            AssignmentRevisionService>();

        services.AddScoped<
            IAssignmentHistoryQueryService,
            AssignmentHistoryQueryService>();

        services.AddScoped<
            IIndividualSubmissionService,
            IndividualSubmissionService>();

        services.AddScoped<
            IIndividualSubmissionQueryService,
            IndividualSubmissionQueryService>();

        services.AddScoped<IIndividualSubmissionReviewService, IndividualSubmissionReviewService>();
        services.AddScoped<ITeamSubmissionService, TeamSubmissionService>();
        services.AddScoped<IAssignmentReopeningService, IndividualSubmissionReviewService>();

        var database = new TestDatabase(
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateScopes = true,
                    ValidateOnBuild = true
                }));

        try
        {
            await using var scope =
                database.Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            await db.GetService<IMigrator>().MigrateAsync(targetMigration);

            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in new[] { "Administrator", "Candidate", "Mentor" })
            {
                var result =
                    await roleManager.CreateAsync(new IdentityRole(role));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ",
                            result.Errors.Select(error => error.Description)));
                }
            }

            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }


    public async Task<AccountCreationResult> CreateAccountAsync(
        string userName,
        string password,
        string role)
    {
        await using var scope = Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IAccountCreationService>();

        if (AccountAdministratorId.Length == 0)
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var administrator = new IdentityUser { UserName = "account.tests.admin" };
            var created = await users.CreateAsync(administrator, "Integration-Only!482");
            if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
            var assigned = await users.AddToRoleAsync(administrator, "Administrator");
            if (!assigned.Succeeded) throw new InvalidOperationException(string.Join("; ", assigned.Errors.Select(e => e.Description)));
            AccountAdministratorId = administrator.Id;
        }
        var result=await service.CreateAsync(userName, password, role, AccountAdministratorId);
        if(result.Succeeded)
        {
            // Tests needing a usable account follow the same activation flow.
            var db=scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var lifecycle=new AccountLifecycleService(db,users);var user=(await users.FindByNameAsync(userName))!;
            var managed=(await lifecycle.GetAsync(user.Id,AccountAdministratorId))!;
            var issued=await lifecycle.IssueAsync(user.Id,"Activation",managed.Version,"Test account activation",AccountAdministratorId);
            if(!issued.Succeeded)throw new InvalidOperationException(issued.Error);
            var activated=await lifecycle.RedeemAsync(userName,issued.Credential!.Credential,password);
            if(!activated.Succeeded)throw new InvalidOperationException(activated.Error);
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        // Fixture initialisation and xUnit teardown can both call disposal after
        // a SQL failure. Preserve the first failure rather than masking it with
        // a second attempt to use the already disposed service provider.
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            await using var scope = Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            await db.Database.EnsureDeletedAsync();
        }
        finally
        {
            await Services.DisposeAsync();
        }
    }
}
