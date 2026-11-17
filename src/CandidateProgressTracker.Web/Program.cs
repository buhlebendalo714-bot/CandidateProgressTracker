using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Infrastructure.Cohorts;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("Credentials", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int?>("Authentication:RequestsPerMinute") ?? 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(

        "Connection string 'DefaultConnection' is missing.");

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<TrackerDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Add services to the container.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdministratorOnly", policy =>
        policy.RequireRole("Administrator"));

    options.AddPolicy("MentorOnly", policy =>
        policy.RequireRole("Mentor"));

    options.AddPolicy("CandidateOnly", policy =>
        policy.RequireRole("Candidate"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder(
        "/Admin", "AdministratorOnly");

    options.Conventions.AuthorizeFolder(
        "/Mentor", "MentorOnly");

    options.Conventions.AuthorizeFolder(
        "/Candidate", "CandidateOnly");
});

builder.Services.AddScoped<IAccountQueryService, AccountQueryService>();

builder.Services.AddScoped<IAccountCreationService, AccountCreationService>();
builder.Services.AddScoped<IAccountLifecycleService, AccountLifecycleService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<IArchivedHistoryService, ArchivedHistoryService>();
builder.Services.AddScoped<SignInManager<IdentityUser>, CandidateProgressTracker.Web.ActiveAccountSignInManager>();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

builder.Services.AddScoped<ICohortCreationService, CohortCreationService>();

builder.Services.AddScoped<ICohortQueryService, CohortQueryService>();

builder.Services.AddScoped<ICohortMembershipService, CohortMembershipService>();
builder.Services.AddScoped<IBulkOnboardingPreviewService, BulkOnboardingPreviewService>();
builder.Services.AddScoped<IBulkOnboardingImportService, BulkOnboardingImportService>();

builder.Services.AddScoped<ICohortMembershipQueryService, CohortMembershipQueryService>();

builder.Services.AddScoped<ICohortMembershipEndingService, CohortMembershipEndingService>();

builder.Services.AddScoped<ICohortArchiveService, CohortArchiveService>();

builder.Services.AddScoped<IMentorCohortQueryService, MentorCohortQueryService>();

builder.Services.AddScoped<ICandidateCohortQueryService, CandidateCohortQueryService>();

builder.Services.AddScoped<ILearningCycleService, LearningCycleService>();

builder.Services.AddScoped<IMemberCycleQueryService, MemberCycleQueryService>();

builder.Services.AddScoped<IProjectService, ProjectService>();

builder.Services.AddScoped<IMilestoneService, MilestoneService>();

builder.Services.AddScoped<IProjectTeamService, ProjectTeamService>();

builder.Services.AddScoped<IMilestoneAssignmentService, MilestoneAssignmentService>();

builder.Services.AddScoped<IMemberAssignmentQueryService, MemberAssignmentQueryService>();

builder.Services.AddScoped<IAssignmentRevisionService, AssignmentRevisionService>();

builder.Services.AddScoped<IAssignmentHistoryQueryService, AssignmentHistoryQueryService>();

builder.Services.AddScoped<IIndividualSubmissionService, IndividualSubmissionService>();
builder.Services.AddScoped<ITeamSubmissionService, TeamSubmissionService>();

builder.Services.AddScoped<IIndividualSubmissionQueryService, IndividualSubmissionQueryService>();
builder.Services.AddScoped<IIndividualSubmissionReviewService, IndividualSubmissionReviewService>();
builder.Services.AddScoped<IAssignmentReopeningService, IndividualSubmissionReviewService>();

builder.Services.Configure<EvidenceOptions>(builder.Configuration.GetSection("Evidence"));
builder.Services.PostConfigure<EvidenceOptions>(o => o.RepositoryRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../..")));
builder.Services.AddSingleton<IPrivateEvidenceStorage, PrivateEvidenceStorage>();
builder.Services.AddSingleton<IMalwareScanner, DefenderScanner>();
builder.Services.AddScoped<IFileEvidenceService, FileEvidenceService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ILearningOverviewService, LearningOverviewService>();
if (!builder.Environment.IsEnvironment("Testing")) builder.Services.AddHostedService<CandidateProgressTracker.Web.EvidenceWorker>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 53477376);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 53477376);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    if(context.User.Identity?.IsAuthenticated==true)
        context.RequestServices.GetRequiredService<TrackerDbContext>().AuditActorId=context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    await next();
    if(context.Response.StatusCode is 401 or 403 or 404 || context.Response.StatusCode == 302 && context.Response.Headers.Location.ToString().Contains("/Account/"))
        app.Logger.LogInformation("Access response {Status} actor {ActorId} path {Path} correlation {CorrelationId}", context.Response.StatusCode, context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, context.Request.Path.Value, context.TraceIdentifier);
});
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = ["Administrator", "Mentor", "Candidate"];

    foreach (var role in roles)
    {
        if (await roleManager.RoleExistsAsync(role))
        {
            continue;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(role));

        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Could not create role '{role}': {errors}");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    var adminPassword = app.Configuration["DevelopmentAdmin:Password"];

    if (!string.IsNullOrWhiteSpace(adminPassword))
    {
        using var scope = app.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        const string adminUserName = "dev.admin";

        var admin = await userManager.FindByNameAsync(adminUserName);

        if (admin is null)
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            await using var transaction =
                await db.Database.BeginTransactionAsync();

            admin = new IdentityUser
            {
                UserName = adminUserName,
                LockoutEnabled = true
            };

            var createResult =
                await userManager.CreateAsync(admin, adminPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    createResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Could not create development administrator: {errors}");
            }

            var roleResult =
                await userManager.AddToRoleAsync(admin, "Administrator");

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    roleResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Could not assign administrator role: {errors}");
            }

            await transaction.CommitAsync();
        }
        else if (!await userManager.IsInRoleAsync(admin, "Administrator"))
        {
            throw new InvalidOperationException(
                "The dev.admin username already exists without the " +
                "Administrator role. No permissions were changed.");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    var password = app.Configuration["DevelopmentCandidate:Password"];

    if (!string.IsNullOrWhiteSpace(password))
    {
        using var scope = app.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        const string userName = "dev.candidate";

        var candidate = await userManager.FindByNameAsync(userName);

        if (candidate is null)
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TrackerDbContext>();

            await using var transaction =
                await db.Database.BeginTransactionAsync();

            candidate = new IdentityUser
            {
                UserName = userName,
                LockoutEnabled = true
            };

            var createResult =
                await userManager.CreateAsync(candidate, password);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    createResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Could not create development candidate: {errors}");
            }

            var roleResult =
                await userManager.AddToRoleAsync(candidate, "Candidate");

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    roleResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Could not assign Candidate role: {errors}");
            }

            await transaction.CommitAsync();
        }

        var assignedRoles = await userManager.GetRolesAsync(candidate);

        if (assignedRoles.Count != 1 ||
            !assignedRoles.Contains("Candidate"))
        {
            throw new InvalidOperationException(
                "dev.candidate must have only the Candidate role for this test.");
        }
    }
}

app.Run();

public partial class Program
{
}
