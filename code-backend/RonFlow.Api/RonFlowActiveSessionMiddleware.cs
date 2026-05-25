using System.Security.Claims;
using RonFlow.Application;
using RonFlow.Observability;

namespace RonFlow.Api;

public sealed class RonFlowActiveSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RonFlowActiveSessionRegistry activeSessionRegistry)
    {
        System.Diagnostics.Stopwatch? stopwatch = null;
        if (ObservedOperationTimingContext.TryGetCurrent(out _))
        {
            stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        if (context.User.Identity?.IsAuthenticated != true || IsSessionActivationRequest(context.Request.Path))
        {
            RecordTiming(stopwatch);
            await next(context);
            return;
        }

        var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        var sessionId = context.Request.Headers[RonFlowSessionConstants.SessionIdHeaderName].FirstOrDefault()
            ?? context.User.FindFirstValue(RonFlowSessionConstants.SessionIdClaimType);

        if (!Guid.TryParse(rawUserId, out var userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            RecordTiming(stopwatch);
            await next(context);
            return;
        }

        if (!activeSessionRegistry.IsActive(userId, sessionId))
        {
            RecordTiming(stopwatch);
            context.Response.Headers[RonFlowSessionConstants.SessionInvalidatedHeaderName] = "true";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "RonFlow session invalidated" });
            return;
        }

        RecordTiming(stopwatch);
        await next(context);
    }

    private static void RecordTiming(System.Diagnostics.Stopwatch? stopwatch)
    {
        if (stopwatch is null)
        {
            return;
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        if (ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
        {
            timingSnapshot!.ActiveSessionElapsedMs = elapsedMs;
            RonFlowObservabilityMetrics.RecordActiveSessionDuration(timingSnapshot.OperationName, elapsedMs);
        }
    }

    private static bool IsSessionActivationRequest(PathString path)
    {
        return string.Equals(path.Value, "/api/session/activate", StringComparison.OrdinalIgnoreCase);
    }
}