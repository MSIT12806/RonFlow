using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RonFlow.Observability;

public sealed class BoardReadServerTimingFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        BoardReadObservabilityContext.Reset();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next();

            stopwatch.Stop();
            RonFlowObservabilityMetrics.RecordBoardControllerDuration(stopwatch.Elapsed.TotalMilliseconds);
            var timingSnapshot = BoardReadObservabilityContext.Current;
            var metrics = new List<string>
            {
                FormatServerTimingMetric("board-controller", stopwatch.Elapsed.TotalMilliseconds),
            };

            AppendServerTimingMetric(metrics, "board-application", timingSnapshot.ApplicationElapsedMs);
            AppendServerTimingMetric(metrics, "board-store", timingSnapshot.StoreElapsedMs);

            context.HttpContext.Response.Headers["Server-Timing"] = string.Join(", ", metrics);
        }
        finally
        {
            BoardReadObservabilityContext.Clear();
        }
    }

    private static void AppendServerTimingMetric(List<string> metrics, string metricName, double? durationMs)
    {
        if (!durationMs.HasValue)
        {
            return;
        }

        metrics.Add(FormatServerTimingMetric(metricName, durationMs.Value));
    }

    private static string FormatServerTimingMetric(string metricName, double durationMs)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{metricName};dur={durationMs:0.###}");
    }
}