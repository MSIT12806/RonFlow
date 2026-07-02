using RonFlow.Infrastructure;

namespace RonFlow.Api;

public sealed class DatabaseSyncBackgroundService(
    IDatabaseSyncCoordinator databaseSyncCoordinator,
    ILogger<DatabaseSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                databaseSyncCoordinator.FlushPendingMutations();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process queued database sync mutations.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
