using RonFlow.Application;

namespace RonFlow.Api;

public sealed class TaskNotificationBackgroundService(
    ProcessTaskNotificationsService notificationService,
    ILogger<TaskNotificationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                notificationService.ProcessPending();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to publish task notifications.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
