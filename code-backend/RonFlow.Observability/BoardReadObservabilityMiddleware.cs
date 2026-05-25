using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace RonFlow.Observability;

public sealed class BoardReadObservabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsBoardReadRequest(context.Request))
        {
            await next(context);
            return;
        }

        BoardReadObservabilityContext.Reset();
        var stopwatch = Stopwatch.StartNew();

        context.Response.OnStarting(() =>
        {
            if (!BoardReadObservabilityContext.TryGetCurrent(out var timingSnapshot))
            {
                return Task.CompletedTask;
            }

            var responseStartElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            timingSnapshot!.ResponseStartElapsedMs = responseStartElapsedMs;
            RonFlowObservabilityMetrics.RecordBoardResponseStartDuration(responseStartElapsedMs);

            var metrics = new List<string>();
            AppendServerTimingMetric(metrics, "board-response-start", responseStartElapsedMs);
            AppendServerTimingMetric(metrics, "board-current-user-sync", timingSnapshot.CurrentUserDirectorySyncElapsedMs);
            AppendServerTimingMetric(metrics, "board-active-session", timingSnapshot.ActiveSessionElapsedMs);
            AppendServerTimingMetric(metrics, "board-controller", timingSnapshot.ControllerElapsedMs);
            AppendServerTimingMetric(metrics, "board-application", timingSnapshot.ApplicationElapsedMs);
            AppendServerTimingMetric(metrics, "board-store", timingSnapshot.StoreElapsedMs);

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
            if (BoardReadObservabilityContext.TryGetCurrent(out var timingSnapshot))
            {
                timingSnapshot!.RequestElapsedMs = elapsedMs;
            }

            RonFlowObservabilityMetrics.RecordBoardRequestDuration(elapsedMs);
            BoardReadObservabilityContext.Clear();
        }
    }

    private static bool IsBoardReadRequest(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var path = request.Path.Value;
        return !string.IsNullOrWhiteSpace(path)
            && path.StartsWith("/api/projects/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/board", StringComparison.OrdinalIgnoreCase);
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