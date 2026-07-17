using RonFlow.Application;
using RonFlow.Infrastructure;

namespace RonFlow.Api;

public sealed class AiAuditProjectionBackgroundService(
    ProcessAiAuditProjectionService projectionService,
    IRuntimeDatabaseAccessGate databaseAccessGate,
    ILogger<AiAuditProjectionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var timer = new PeriodicTimer(PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var lease = await databaseAccessGate.EnterReadAsync(stoppingToken);
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
