using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RonFlow.Observability;

public sealed class BoardReadServerTimingFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();

        await next();

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        RonFlowObservabilityMetrics.RecordBoardControllerDuration(elapsedMs);
        if (BoardReadObservabilityContext.TryGetCurrent(out var timingSnapshot))
        {
            timingSnapshot!.ControllerElapsedMs = elapsedMs;
        }
    }
}