using RonFlow.Application;

namespace RonFlow.Api;

public sealed class ReminderNotificationBackgroundService(
    DeliverDueReminderNotificationsCommandService commandService,
    ILogger<ReminderNotificationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                commandService.DeliverDueReminders();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to deliver due reminder notifications.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}