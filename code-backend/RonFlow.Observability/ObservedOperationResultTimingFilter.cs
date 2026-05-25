using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RonFlow.Observability;

public sealed class ObservedOperationResultTimingFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next();
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            timingSnapshot!.ResultElapsedMs = elapsedMs;
            RonFlowObservabilityMetrics.RecordResultDuration(timingSnapshot.OperationName, elapsedMs);
        }
    }
}