using RonFlow.Application;
using RonFlow.Infrastructure;

namespace RonFlow.Api;

public sealed class ReminderNotificationBackgroundService(
    DeliverDueReminderNotificationsCommandService commandService,
    IRuntimeDatabaseAccessGate databaseAccessGate,
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
                using var lease = await databaseAccessGate.EnterReadAsync(stoppingToken);
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
