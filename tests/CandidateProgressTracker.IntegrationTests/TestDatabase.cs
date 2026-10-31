using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Cohorts;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateProgressTracker.IntegrationTests;

internal sealed class TestDatabase : IAsyncDisposable
{
    public ServiceProvider Services { get; }

    private TestDatabase(ServiceProvider services)
    {
        Services = services;
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        var databaseName =
            $"CandidateProgressTracker_Tests_{Guid.NewGuid():N}";

        var connectionString =
            $@"Server=.\SQLEXPRESS;Database={databaseName};" +
            "Trusted_Connection=True;Encrypt=True;" +
            "TrustServerCertificate=True;";

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddDbContext<TrackerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TrackerDbContext>();

        services.AddScoped<
            IAccountCreationService,
            AccountCreationService>();

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

            await db.Database.MigrateAsync();

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

        return await service.CreateAsync(userName, password, role);
    }

    public async ValueTask DisposeAsync()
    {
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