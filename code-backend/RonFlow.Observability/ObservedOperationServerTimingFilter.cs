using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RonFlow.Observability;

public sealed class ObservedOperationServerTimingFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        await next();

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        timingSnapshot!.ControllerElapsedMs = elapsedMs;
        RonFlowObservabilityMetrics.RecordControllerDuration(timingSnapshot.OperationName, elapsedMs);
    }
}