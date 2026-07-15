using RonFlow.Infrastructure;

namespace RonFlow.Api;

public sealed class DatabaseSyncBackgroundService(
    IDatabaseSyncCoordinator databaseSyncCoordinator,
    ILogger<DatabaseSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "RonFlow database Git sync background service started. PollingIntervalSeconds: {PollingIntervalSeconds}",
            PollingInterval.TotalSeconds);

        using var timer = new PeriodicTimer(PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                databaseSyncCoordinator.RequestPullIfStale("scheduled pull refresh");

                if (databaseSyncCoordinator.FlushPendingPullRequests())
                {
                    logger.LogInformation("RonFlow database Git sync background service processed queued pull refresh.");
                }

                if (databaseSyncCoordinator.FlushPendingMutations())
                {
                    logger.LogInformation("RonFlow database Git sync background service finished a queued mutation batch.");
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process queued database sync mutations.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
