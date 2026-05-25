using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace RonFlow.Observability;

public sealed class ObservedOperationTimingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<ObservedOperationAttribute>();
        if (metadata is null)
        {
            await next(context);
            return;
        }

        ObservedOperationTimingContext.Reset(metadata.OperationName);
        var stopwatch = Stopwatch.StartNew();

        context.Response.OnStarting(() =>
        {
            if (!ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
            {
                return Task.CompletedTask;
            }

            var responseStartElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            timingSnapshot!.ResponseStartElapsedMs = responseStartElapsedMs;
            RonFlowObservabilityMetrics.RecordResponseStartDuration(timingSnapshot.OperationName, responseStartElapsedMs);

            var metrics = new List<string>();
            AppendServerTimingMetric(metrics, "response-start", responseStartElapsedMs);
            AppendServerTimingMetric(metrics, "middleware-current-user-sync", timingSnapshot.CurrentUserDirectorySyncElapsedMs);
            AppendServerTimingMetric(metrics, "middleware-current-user-sync-lookup", timingSnapshot.CurrentUserDirectorySyncLookupElapsedMs);
            AppendServerTimingMetric(metrics, "middleware-current-user-sync-upsert", timingSnapshot.CurrentUserDirectorySyncUpsertElapsedMs);
            AppendServerTimingMetric(metrics, "middleware-current-user-sync-save", timingSnapshot.CurrentUserDirectorySyncSaveElapsedMs);
            AppendServerTimingMetric(metrics, "middleware-active-session", timingSnapshot.ActiveSessionElapsedMs);
            AppendServerTimingMetric(metrics, "controller", timingSnapshot.ControllerElapsedMs);
            AppendServerTimingMetric(metrics, "application", timingSnapshot.ApplicationElapsedMs);
            AppendServerTimingMetric(metrics, "store", timingSnapshot.StoreElapsedMs);

            if (metrics.Count > 0)
            {
                context.Response.Headers["Server-Timing"] = string.Join(", ", metrics);
            }

            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            if (ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
            {
                timingSnapshot!.RequestElapsedMs = elapsedMs;
                RonFlowObservabilityMetrics.RecordRequestDuration(timingSnapshot.OperationName, elapsedMs);
            }

            ObservedOperationTimingContext.Clear();
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