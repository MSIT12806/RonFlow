using RonFlow.Application;

namespace RonFlow.Api;

public sealed class AiAuditProjectionBackgroundService(
    ProcessAiAuditProjectionService projectionService,
    ILogger<AiAuditProjectionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                projectionService.ProcessPending();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process AI audit projection messages.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}