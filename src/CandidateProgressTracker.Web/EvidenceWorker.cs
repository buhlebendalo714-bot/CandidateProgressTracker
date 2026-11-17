using CandidateProgressTracker.Application.Learning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace CandidateProgressTracker.Web;
public sealed class EvidenceWorker(IServiceScopeFactory scopes,ILogger<EvidenceWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextCleanup=DateTimeOffset.UtcNow.AddHours(1);
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope=scopes.CreateAsyncScope(); var service=scope.ServiceProvider.GetRequiredService<IFileEvidenceService>();
                if(DateTimeOffset.UtcNow>=nextCleanup) { await service.CleanupAsync(stoppingToken); nextCleanup=DateTimeOffset.UtcNow.AddHours(1); }
                if(await service.ProcessNextAsync(stoppingToken)) continue;
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception e) { log.LogError("Evidence processing unavailable ({FailureType}); durable work will be retried.",e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(15),stoppingToken);
        }
    }
}
