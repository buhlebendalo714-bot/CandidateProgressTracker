using System.Net;
using System.Text.RegularExpressions;
using CandidateProgressTracker.Application.Learning;
using CandidateProgressTracker.Domain.Learning;
using CandidateProgressTracker.Infrastructure.Learning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace CandidateProgressTracker.IntegrationTests;

public sealed class CandidateDashboardTests(WebAccessFixture fixture):IClassFixture<WebAccessFixture>
{
    [Fact]
    public async Task Dashboard_prioritises_candidate_work_encodes_scoped_feedback_and_keeps_pending_reviews_out_of_actions()
    {
        await using var t=await ReviewTestContext.CreateAsync(fixture);
        var cycle=await t.Db.LearningCycles.SingleAsync(c=>c.CycleId==t.Db.Assignments.Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.CycleId).First());
        cycle.Name="Foundation programme";var cohort=await t.Db.Cohorts.SingleAsync(c=>c.CohortId==t.Seed.CohortId);cohort.Name="Digital skills cohort";
        var milestone=await t.Db.Milestones.SingleAsync(m=>m.MilestoneId==t.Db.Assignments.Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.MilestoneId).First());milestone.Title="Build your project evidence";await t.Db.SaveChangesAsync();
        var feedback="Clarify your approach and include the results. <script>alert('unsafe')</script>";
        Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(ReviewDecision.ChangesRequested,feedback),t.MentorId)).Succeeded);
        var courses=t.Services.GetRequiredService<ICourseService>();var title="Cloud foundations "+Guid.NewGuid().ToString("N")[..6];
        Assert.Null(await courses.CreateAsync(title,"Theory learning","https://example.com/course",t.Seed.AdministratorId));var course=await t.Db.Courses.SingleAsync(c=>c.Title==title);
        Assert.Null(await courses.OfferAsync(course.CourseId,cycle.CycleId,t.Seed.AdministratorId));var offering=await t.Db.CourseOfferings.SingleAsync(o=>o.CourseId==course.CourseId);
        Assert.Null(await courses.AssignAsync(offering.OfferingId,t.Seed.CandidateId,DateTimeOffset.UtcNow.AddDays(3),"Complete course","Certificate","Upload certificate",true,t.Seed.AdministratorId));
        using var candidate=await fixture.CreateClientAsync("Candidate",t.Seed.CandidateId);var path=$"/Candidate?cycleId={cycle.CycleId}";
        using var response=await candidate.GetAsync(path);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var html=await response.Content.ReadAsStringAsync();
        Assert.Contains("Your progress",html);Assert.Contains("Courses &amp; certificates",html);Assert.Contains("Work to focus on",html);Assert.Contains("Recent feedback",html);
        Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>alert('unsafe')</script>",html);Assert.DoesNotContain("People &amp; access",html);
        var actions=Regex.Matches(html,"class=\"candidate-action\" data-assignment-id=\"([^\"]+)\"");Assert.Equal(2,actions.Count);Assert.Equal(t.Seed.AssignmentId.ToString(),actions[0].Groups[1].Value);
        var data=(await t.Services.GetRequiredService<ILearningOverviewService>().GetCandidateAsync(t.Seed.CandidateId,cycle.CycleId))!;Assert.Single(data.RecentFeedback);Assert.Equal(feedback,data.RecentFeedback[0].Feedback);

        // Simulate an elapsed original deadline, not a forbidden admin backdate.
        await using var pending=await ReviewTestContext.CreateAsync(fixture,dueAt:DateTimeOffset.UtcNow.AddDays(-1));
        var pendingCycle=await pending.Db.Assignments.Where(a=>a.AssignmentId==pending.Seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();
        using var pendingResponse=await candidate.GetAsync($"/Candidate?cycleId={pendingCycle}");var pendingHtml=await pendingResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("class=\"candidate-action\"",pendingHtml);Assert.Contains("1 awaiting mentor review",pendingHtml);Assert.DoesNotContain("Overdue submission",pendingHtml);Assert.DoesNotContain("&lt;script&gt;",pendingHtml);
        Assert.Equal(HttpStatusCode.NotFound,(await candidate.GetAsync($"/Candidate?cycleId={Guid.NewGuid()}")).StatusCode);
        if(Environment.GetEnvironmentVariable("CPT_DASHBOARD_VISUAL")=="1")await VerifyBrowserAsync(candidate,path,html);
    }

    [Fact]
    public async Task Shared_feedback_is_readable_by_current_joiners_but_not_credited_leavers_and_never_enables_joiner_actions()
    {
        await using var t=await TeamTestContext.CreateAsync(fixture);await t.SubmitAsync();
        const string feedback="Shared team feedback";Assert.True((await t.Reviews.DecideAsync(await t.DecisionAsync(ReviewDecision.ChangesRequested,feedback),t.MentorId)).Succeeded);
        var cycle=await t.Db.Assignments.Where(a=>a.AssignmentId==t.Seed.AssignmentId).Select(a=>a.CycleId).SingleAsync();var service=new LearningOverviewService(t.Db);
        Assert.Single((await service.GetCandidateAsync(t.OtherId,cycle))!.RecentFeedback);
        var joiner=await t.AddUserAsync();var joined=(await service.GetCandidateAsync(joiner,cycle))!;Assert.Single(joined.RecentFeedback);Assert.False(joined.Assignments.Single().CanSubmit);
        using var client=await fixture.CreateClientAsync("Candidate",joiner);var html=await client.GetStringAsync($"/Candidate?cycleId={cycle}");Assert.Contains(feedback,html);Assert.DoesNotContain("class=\"candidate-action\"",html);
        await t.EndTeamAsync(t.OtherId);var ended=(await service.GetCandidateAsync(t.OtherId,cycle))!;Assert.Empty(ended.RecentFeedback);Assert.Empty(ended.Assignments);
        using var former=await fixture.CreateClientAsync("Candidate",t.OtherId);Assert.DoesNotContain(feedback,await former.GetStringAsync($"/Candidate?cycleId={cycle}"));
    }

    internal static async Task VerifyBrowserAsync(HttpClient candidate,string path,string html, string script = "candidate-dashboard.cjs", string artifacts = "dashboard-visual", string? browserPath = null)
    {
        // Render the real authorised HTTP response and its real assets as a stable
        // visual snapshot. GET-only loopback, fictional records in this fixture's
        // unique database; no Development identity, cookies or data used. Fetching
        // sequentially also avoids simultaneous DB cookie checks for static assets.
        if(Environment.GetEnvironmentVariable("CPT_BRANDING_VISUAL")=="1")
        {
            script="dashboard-branding.cjs";
            artifacts="dashboard-branding/"+path.Split('?')[0].Trim('/').ToLowerInvariant();
        }
        var snapshots=new Dictionary<string,(byte[] Bytes,string Type)>(StringComparer.Ordinal)
        { [path]=(System.Text.Encoding.UTF8.GetBytes(html),"text/html; charset=utf-8") };
        foreach(var asset in Regex.Matches(html,"(?:href|src)=\"([^\"]+\\.(?:css|js|webp)(?:\\?[^\"]*)?)\"").Select(m=>System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).Where(url=>url.StartsWith("/",StringComparison.Ordinal)).Distinct())
        {
            using var response=await candidate.GetAsync(asset);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            snapshots[asset]=(await response.Content.ReadAsByteArrayAsync(),response.Content.Headers.ContentType?.ToString()??"application/octet-stream");
        }
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:7259");
        await using var bridge=builder.Build();bridge.Run(async context=>
        {
            if(!HttpMethods.IsGet(context.Request.Method)){context.Response.StatusCode=405;return;}
            var key=context.Request.Path.Value+context.Request.QueryString.Value;
            if(!snapshots.TryGetValue(key,out var snapshot)&&!snapshots.TryGetValue(context.Request.Path.Value!,out snapshot)){context.Response.StatusCode=404;return;}
            context.Response.ContentType=snapshot.Type;await context.Response.Body.WriteAsync(snapshot.Bytes);
        });
        await bridge.StartAsync();
        try
        {
            var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"AGENTS.md")))root=root.Parent;Assert.NotNull(root);
            var start=new System.Diagnostics.ProcessStartInfo("node"){WorkingDirectory=root!.FullName,UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add("tests/CandidateProgressTracker.IntegrationTests/Browser/"+script);start.Environment["CPT_DASHBOARD_PATH"]=path;
            var outputDirectory=Path.Combine(root.FullName,"TestResults",artifacts);Directory.CreateDirectory(outputDirectory);
            start.Environment["CPT_DASHBOARD_OUTPUT"]=outputDirectory;
              if(browserPath is not null) start.Environment["CHROME_PATH"]=browserPath;
            await File.WriteAllTextAsync(Path.Combine(outputDirectory,"authenticated-snapshot.json"),System.Text.Json.JsonSerializer.Serialize(new { Path=path,Pages=snapshots.ToDictionary(x=>x.Key,x=>new {Body=Convert.ToBase64String(x.Value.Bytes),x.Value.Type}) }));
            start.RedirectStandardOutput=true;start.RedirectStandardError=true;
            using var process=System.Diagnostics.Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
              try{await process.WaitForExitAsync(timeout.Token);}catch
              {
                  if(!process.HasExited)process.Kill(true);
                  await process.WaitForExitAsync();
                  await File.WriteAllTextAsync(Path.Combine(outputDirectory,"browser-output.txt"),(await stdout)+Environment.NewLine+(await stderr));
                  throw;
              }
            var diagnostics=(await stdout)+Environment.NewLine+(await stderr);await File.WriteAllTextAsync(Path.Combine(outputDirectory,"browser-output.txt"),diagnostics);
            Assert.True(process.ExitCode==0,diagnostics);
        }
        finally{await bridge.StopAsync();}
    }
}
