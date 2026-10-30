using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddRazorPages();

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

app.UseAuthentication();
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
