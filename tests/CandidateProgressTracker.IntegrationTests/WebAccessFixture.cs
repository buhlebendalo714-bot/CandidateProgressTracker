using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

internal sealed class AccessWebFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public AccessWebFactory(string connectionString)
    {
        var databaseName =
            new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        if (!databaseName.StartsWith(
            "CandidateProgressTracker_Tests_",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The web test host requires a temporary test database.");
        }

        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            _connectionString);

        builder.ConfigureTestServices(services =>
        {
            // Remove both the context and its existing options callbacks.
            services.RemoveAll<TrackerDbContext>();
            services.RemoveAll<DbContextOptions<TrackerDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<TrackerDbContext>>();

            services.AddDbContext<TrackerDbContext>(options =>
                options.UseSqlServer(_connectionString));
        });
    }
}

public sealed class WebAccessFixture : IAsyncLifetime
{
    private TestDatabase? _database;
    private AccessWebFactory? _factory;

    private readonly Dictionary<string, string> _userIds = new();

    public Guid AssignmentId { get; private set; }

    public Guid MilestoneId { get; private set; }

    public IServiceProvider Services =>
        _factory?.Services
        ?? throw new InvalidOperationException("Fixture is not initialized.");

    public async Task InitializeAsync()
    {
        try
        {
            _database = await TestDatabase.CreateAsync();

            string connectionString;

            await using (var scope =
                _database.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<TrackerDbContext>();

                connectionString =
                    db.Database.GetDbConnection().ConnectionString;
            }

            _factory = new AccessWebFactory(connectionString);

            // Starts the test host after the temporary database is ready.
            var environment = Services.GetRequiredService<IHostEnvironment>();

            if (environment.EnvironmentName != "Testing")
            {
                throw new InvalidOperationException(
                    "The web test host must run in Testing.");
            }

            await using var webScope = Services.CreateAsyncScope();

            var webDb = webScope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            var actualConnection =
                new SqlConnectionStringBuilder(
                    webDb.Database.GetDbConnection().ConnectionString);

            var expectedConnection =
                new SqlConnectionStringBuilder(connectionString);

            if (actualConnection.InitialCatalog !=
                expectedConnection.InitialCatalog)
            {
                throw new InvalidOperationException(
                    "The web host is not using the fixture database.");
            }

            var users = webScope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            foreach (var role in new[]
            {
                "Administrator",
                "Candidate",
                "Mentor"
            })
            {
                var user = new IdentityUser
                {
                    UserName = $"web.{role}.{Guid.NewGuid():N}"
                };

                var created = await users.CreateAsync(
                    user, "Integration-Only!482");

                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ",
                            created.Errors.Select(error => error.Description)));
                }

                var assigned = await users.AddToRoleAsync(user, role);

                if (!assigned.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ",
                            assigned.Errors.Select(error => error.Description)));
                }

                _userIds.Add(role, user.Id);
            }

            await SeedAssignmentAsync(webDb);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    private async Task SeedAssignmentAsync(TrackerDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var adminId = _userIds["Administrator"];
        var candidateId = _userIds["Candidate"];

        var cohort = new Cohort
        {
            Name = "HTTP access cohort",
            Code = $"HTTP-{Guid.NewGuid():N}",
            CreatedAt = now,
            CreatedBy = adminId
        };

        var cycle = new LearningCycle
        {
            CohortId = cohort.CohortId,
            Name = "HTTP access cycle",
            StartsOn = DateOnly.FromDateTime(now.UtcDateTime),
            EndsOn = DateOnly.FromDateTime(now.UtcDateTime).AddMonths(3),
            CreatedAt = now,
            CreatedBy = adminId
        };

        var project = new Project
        {
            CycleId = cycle.CycleId,
            Title = "HTTP access project",
            Brief = "Web authorization test",
            Mode = ProjectMode.Individual,
            CreatedAt = now,
            CreatedBy = adminId
        };

        var milestone = new Milestone
        {
            ProjectId = project.ProjectId,
            SequenceNo = 1,
            Title = "HTTP access milestone",
            Instructions = "HTTP test instructions",
            AcceptanceExpectations = "HTTP test expectations",
            EvidencePolicy = "HTTP test evidence",
            CreatedAt = now,
            CreatedBy = adminId
        };

        var assignment = new Assignment
        {
            CycleId = cycle.CycleId,
            ProjectId = project.ProjectId,
            MilestoneId = milestone.MilestoneId,
            CandidateId = candidateId,
            IsRequired = true,
            DueAt = now.AddDays(7),
            CreatedAt = now,
            CreatedBy = adminId
        };

        var revision = new AssignmentRevision
        {
            AssignmentId = assignment.AssignmentId,
            RevisionNo = 1,
            DueAt = assignment.DueAt,
            InstructionsSnapshot = milestone.Instructions,
            AcceptanceSnapshot = milestone.AcceptanceExpectations,
            EvidencePolicySnapshot = milestone.EvidencePolicy,
            ChangedAt = now,
            ChangedBy = adminId,
            Reason = "Initial assignment"
        };

        db.Cohorts.Add(cohort);
        db.LearningCycles.Add(cycle);
        db.Projects.Add(project);
        db.Milestones.Add(milestone);
        db.Assignments.Add(assignment);
        db.AssignmentRevisions.Add(revision);

        foreach (var member in new[]
        {
            (UserId: candidateId, Kind: MembershipKind.Candidate),
            (UserId: _userIds["Mentor"], Kind: MembershipKind.Mentor)
        })
        {
            db.CohortMemberships.Add(new CohortMembership
            {
                CohortId = cohort.CohortId,
                UserId = member.UserId,
                MembershipKind = member.Kind,
                StartsAt = now.AddDays(-1),
                ChangeReason = "HTTP access test",
                CreatedBy = adminId
            });
        }

        await db.SaveChangesAsync();

        AssignmentId = assignment.AssignmentId;
        MilestoneId = milestone.MilestoneId;
    }

    public async Task<HttpClient> CreateClientAsync(string? role = null)
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture is not initialized.");
        }

        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });

        if (role is null)
        {
            return client;
        }

        try
        {
            await using var scope = Services.CreateAsyncScope();

            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            var user = await users.FindByIdAsync(_userIds[role])
                ?? throw new InvalidOperationException("Test user is missing.");

            var principalFactory = scope.ServiceProvider
                .GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>();

            var principal = await principalFactory.CreateAsync(user);

            var options = scope.ServiceProvider
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);

            var now = DateTimeOffset.UtcNow;

            var ticket = new AuthenticationTicket(
                principal,
                new AuthenticationProperties
                {
                    IssuedUtc = now,
                    ExpiresUtc = now.AddHours(1)
                },
                IdentityConstants.ApplicationScheme);

            var cookie = options.TicketDataFormat.Protect(ticket);

            client.DefaultRequestHeaders.Add(
                "Cookie",
                $"{options.Cookie.Name}={cookie}");

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }
        }
        finally
        {
            if (_database is not null)
            {
                await _database.DisposeAsync();
                _database = null;
            }
        }
    }
}