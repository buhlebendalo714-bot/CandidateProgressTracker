using System.Net;
using System.Text.RegularExpressions;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CandidateProgressTracker.IntegrationTests;

public sealed class SubmissionPageTests
    : IClassFixture<WebAccessFixture>
{
    private readonly WebAccessFixture _fixture;

    public SubmissionPageTests(WebAccessFixture fixture)
    {
        _fixture = fixture;
    }

    private string SubmitPath =>
        $"/Candidate/Submit/{_fixture.AssignmentId}";

    [Theory]
    [InlineData(null, "/Account/Login")]
    [InlineData("Mentor", "/Account/AccessDenied")]
    public async Task Unauthorized_user_is_redirected(
        string? role,
        string expectedPath)
    {
        using var client = await _fixture.CreateClientAsync(role);
        using var response = await client.GetAsync(SubmitPath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var destination = new Uri(
            client.BaseAddress!,
            response.Headers.Location);

        Assert.Equal(expectedPath, destination.AbsolutePath);
    }

    [Fact]
    public async Task Candidate_can_open_form_with_requirements_and_tokens()
    {
        using var client =
            await _fixture.CreateClientAsync("Candidate");

        using var response = await client.GetAsync(SubmitPath);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("HTTP test instructions", html);
        Assert.Contains("HTTP test expectations", html);
        Assert.Contains("HTTP test evidence", html);

        Assert.NotEmpty(Hidden(html, "__RequestVerificationToken"));
        Assert.NotEqual(
            Guid.Empty,
            Guid.Parse(Hidden(html, "Input.RequestId")));
        Assert.NotEqual(
            Guid.Empty,
            Guid.Parse(Hidden(html, "Input.RevisionId")));
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        using var client =
            await _fixture.CreateClientAsync("Candidate");

        var before = await SubmissionCountAsync();

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.RequestId"] = Guid.NewGuid().ToString(),
                ["Input.RevisionId"] = Guid.NewGuid().ToString(),
                ["Input.Explanation"] = "Missing antiforgery token.",
                ["Input.Evidence[0].Label"] = "Evidence",
                ["Input.Evidence[0].Url"] = "https://example.com/work"
            });

        using var response = await client.PostAsync(SubmitPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await SubmissionCountAsync());
    }

    [Fact]
    public async Task Valid_post_is_saved_once_and_stale_new_request_is_rejected()
    {
        using var client =
            await _fixture.CreateClientAsync("Candidate");

        using var formResponse = await client.GetAsync(SubmitPath);
        Assert.Equal(HttpStatusCode.OK, formResponse.StatusCode);

        var html = await formResponse.Content.ReadAsStringAsync();

        // The fixture disables automatic cookie handling.
        // Preserve its authentication cookie and add the antiforgery cookie.
        CopyResponseCookies(client, formResponse);

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] =
                Hidden(html, "__RequestVerificationToken"),
            ["Input.RequestId"] = Hidden(html, "Input.RequestId"),
            ["Input.RevisionId"] = Hidden(html, "Input.RevisionId"),
            ["Input.Explanation"] = "Completed work through the HTTP form.",
            ["Input.Evidence[0].Label"] = "Evidence",
            ["Input.Evidence[0].Url"] = "https://example.com/http-evidence"
        };

        var before = await SubmissionCountAsync();

        using (var content = new FormUrlEncodedContent(fields))
        using (var response = await client.PostAsync(SubmitPath, content))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var destination = new Uri(
                client.BaseAddress!,
                response.Headers.Location);

            Assert.Equal(SubmitPath, destination.AbsolutePath);

            CopyResponseCookies(client, response);
        }

        Assert.Equal(before + 1, await SubmissionCountAsync());

        // Follow the redirect with the TempData cookie to see the receipt.
        using (var confirmation = await client.GetAsync(SubmitPath))
        {
            var confirmationHtml =
                await confirmation.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
            Assert.Contains("Submission received.", confirmationHtml);
            Assert.Contains("Receipt:", confirmationHtml);
        }

        // Send the exact original form again: no additional attempt.
        using (var content = new FormUrlEncodedContent(fields))
        using (var response = await client.PostAsync(SubmitPath, content))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        Assert.Equal(before + 1, await SubmissionCountAsync());

        // Simulate an administrator publishing new requirements.
        await AddRevisionAsync();

        // A retry of the already accepted request remains idempotent.
        using (var content = new FormUrlEncodedContent(fields))
        using (var response = await client.PostAsync(SubmitPath, content))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        Assert.Equal(before + 1, await SubmissionCountAsync());

        // A NEW request using the old revision must be rejected.
        fields["Input.RequestId"] = Guid.NewGuid().ToString();

        using (var content = new FormUrlEncodedContent(fields))
        using (var response = await client.PostAsync(SubmitPath, content))
        {
            var rejectedHtml = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "The assignment requirements have changed.",
                rejectedHtml);

            // Preserve the original token rather than silently switching
            // the candidate's submission to unseen requirements.
            Assert.Equal(
                fields["Input.RevisionId"],
                Hidden(rejectedHtml, "Input.RevisionId"));
        }

        Assert.Equal(before + 1, await SubmissionCountAsync());
    }

    private async Task<int> SubmissionCountAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        return await db.Submissions.CountAsync(
            item => item.AssignmentId == _fixture.AssignmentId);
    }

    private async Task AddRevisionAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();

        var previous = await db.AssignmentRevisions
            .AsNoTracking()
            .Where(item => item.AssignmentId == _fixture.AssignmentId)
            .OrderByDescending(item => item.RevisionNo)
            .FirstAsync();

        db.AssignmentRevisions.Add(new AssignmentRevision
        {
            AssignmentId = previous.AssignmentId,
            RevisionNo = previous.RevisionNo + 1,
            DueAt = previous.DueAt,
            InstructionsSnapshot = previous.InstructionsSnapshot,
            AcceptanceSnapshot = previous.AcceptanceSnapshot,
            EvidencePolicySnapshot = previous.EvidencePolicySnapshot,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = previous.ChangedBy,
            Reason = "HTTP stale-form test"
        });

        await db.SaveChangesAsync();
    }

    internal static string Hidden(string html, string name)
    {
        foreach (Match input in Regex.Matches(
                     html,
                     @"<input\b[^>]*>",
                     RegexOptions.IgnoreCase))
        {
            var nameMatch = Regex.Match(
                input.Value,
                @"\bname=""([^""]*)""",
                RegexOptions.IgnoreCase);

            if (!nameMatch.Success ||
                WebUtility.HtmlDecode(nameMatch.Groups[1].Value) != name)
            {
                continue;
            }

            var valueMatch = Regex.Match(
                input.Value,
                @"\bvalue=""([^""]*)""",
                RegexOptions.IgnoreCase);

            Assert.True(valueMatch.Success, $"Missing value for {name}.");

            return WebUtility.HtmlDecode(valueMatch.Groups[1].Value);
        }

        throw new InvalidOperationException(
            $"The form does not contain {name}.");
    }

    internal static void CopyResponseCookies(
        HttpClient client,
        HttpResponseMessage response)
    {
        var cookies = new Dictionary<string, string>(
            StringComparer.Ordinal);

        if (client.DefaultRequestHeaders.TryGetValues(
                "Cookie", out var existing))
        {
            foreach (var header in existing)
            {
                foreach (var part in header.Split(';'))
                {
                    AddCookie(part);
                }
            }
        }

        if (response.Headers.TryGetValues("Set-Cookie", out var added))
        {
            foreach (var header in added)
            {
                AddCookie(header.Split(';')[0]);
            }
        }

        client.DefaultRequestHeaders.Remove("Cookie");

        if (cookies.Count > 0)
        {
            client.DefaultRequestHeaders.Add(
                "Cookie",
                string.Join("; ", cookies.Select(
                    item => $"{item.Key}={item.Value}")));
        }

        void AddCookie(string value)
        {
            var separator = value.IndexOf('=');

            if (separator <= 0)
            {
                return;
            }

            cookies[value[..separator].Trim()] =
                value[(separator + 1)..].Trim();
        }
    }
}
