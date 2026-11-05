using System.Net;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public class WebAccessTests : IClassFixture<WebAccessFixture>
{
    private readonly WebAccessFixture _fixture;

    public WebAccessTests(WebAccessFixture fixture)
    {
        _fixture = fixture;
    }

    private string ResolvePath(string page)
    {
        return page switch
        {
            "candidate" => "/Candidate/Assignments",
            "mentor" => "/Mentor/Assignments",
            "admin-list" =>
                $"/Admin/Projects/Assignments/{_fixture.MilestoneId}",
            "admin-history" =>
                $"/Admin/Projects/AssignmentHistory/{_fixture.AssignmentId}",
            "admin-revise" =>
                $"/Admin/Projects/ReviseAssignment/{_fixture.AssignmentId}",
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };
    }

    private static void AssertRedirect(
        HttpResponseMessage response,
        string destinationPath,
        string requestedPath)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = new Uri(
            new Uri("https://localhost"),
            response.Headers.Location!);

        Assert.Equal(destinationPath, location.AbsolutePath);

        var query = QueryHelpers.ParseQuery(location.Query);

        Assert.True(query.TryGetValue("ReturnUrl", out var returnUrl));
        Assert.Equal(requestedPath, returnUrl.ToString());
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("mentor")]
    [InlineData("admin-list")]
    [InlineData("admin-history")]
    [InlineData("admin-revise")]
    public async Task Anonymous_visitors_are_redirected_to_login(string page)
    {
        using var client = await _fixture.CreateClientAsync();
        var path = ResolvePath(page);

        using var response = await client.GetAsync(path);

        AssertRedirect(response, "/Account/Login", path);
    }

    [Theory]
    [InlineData("Candidate", "mentor")]
    [InlineData("Candidate", "admin-history")]
    [InlineData("Candidate", "admin-revise")]
    [InlineData("Mentor", "candidate")]
    [InlineData("Mentor", "admin-history")]
    [InlineData("Mentor", "admin-revise")]
    public async Task Wrong_role_is_redirected_to_access_denied(
        string role,
        string page)
    {
        using var client = await _fixture.CreateClientAsync(role);
        var path = ResolvePath(page);

        using var response = await client.GetAsync(path);

        AssertRedirect(response, "/Account/AccessDenied", path);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("HTTP test instructions", body);
    }

    [Theory]
    [InlineData("Candidate", "candidate")]
    [InlineData("Mentor", "mentor")]
    public async Task Correct_member_role_can_render_assignments(
        string role,
        string page)
    {
        using var client = await _fixture.CreateClientAsync(role);

        using var response = await client.GetAsync(ResolvePath(page));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("HTTP access milestone", body);
        Assert.Contains("HTTP test instructions", body);
        Assert.Contains("HTTP test expectations", body);
        Assert.Contains("HTTP test evidence", body);
    }

    [Fact]
    public async Task Administrator_can_render_existing_assignment_history()
    {
        using var client = await _fixture.CreateClientAsync("Administrator");

        using var response = await client.GetAsync(
            ResolvePath("admin-history"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("HTTP access milestone", body);
        Assert.Contains("Initial assignment", body);
        Assert.Contains("HTTP test instructions", body);
    }

    [Fact]
    public async Task Administrator_gets_not_found_for_unknown_assignment()
    {
        using var client = await _fixture.CreateClientAsync("Administrator");

        using var response = await client.GetAsync(
            $"/Admin/Projects/AssignmentHistory/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Web_host_uses_test_environment_and_temporary_database()
    {
        var environment = _fixture.Services
            .GetRequiredService<IHostEnvironment>();

        Assert.Equal("Testing", environment.EnvironmentName);

        await using var scope = _fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        Assert.StartsWith(
            "CandidateProgressTracker_Tests_",
            db.Database.GetDbConnection().Database);

        Assert.False(await db.Users.AnyAsync(user =>
            user.UserName == "dev.admin" ||
            user.UserName == "dev.candidate"));
    }
}