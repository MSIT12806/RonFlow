using RonFlow.Application;

namespace RonFlow.Api;

public sealed class WorkflowThroughputProjectionBackgroundService(
    ProcessWorkflowThroughputProjectionService projectionService,
    ILogger<WorkflowThroughputProjectionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

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
                logger.LogError(exception, "Failed to process workflow throughput projection messages.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}