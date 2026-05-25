using System.Security.Claims;
using RonFlow.Domain;
using RonFlow.Observability;

namespace RonFlow.Api;

internal sealed class CurrentUserDirectorySyncMiddleware(RequestDelegate next)
{
    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, IUserDirectory userDirectory)
    {
        System.Diagnostics.Stopwatch? stopwatch = null;
        if (ObservedOperationTimingContext.TryGetCurrent(out _))
        {
            stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            var userName = context.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var email = context.User.FindFirstValue(ClaimTypes.Email) ?? context.User.FindFirstValue("email") ?? string.Empty;

            if (Guid.TryParse(rawUserId, out var userId)
                && !string.IsNullOrWhiteSpace(userName)
                && !string.IsNullOrWhiteSpace(email))
            {
                var syncTimings = userDirectory.SynchronizeCurrentUser(new KnownUser(userId, userName, email));
                if (ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
                {
                    timingSnapshot!.CurrentUserDirectorySyncLookupElapsedMs = syncTimings.LookupElapsedMs;
                    timingSnapshot.CurrentUserDirectorySyncUpsertElapsedMs = syncTimings.UpsertElapsedMs;
                    timingSnapshot.CurrentUserDirectorySyncSaveElapsedMs = syncTimings.SaveElapsedMs;
                    RonFlowObservabilityMetrics.RecordCurrentUserDirectorySyncLookupDuration(timingSnapshot.OperationName, syncTimings.LookupElapsedMs);
                    RonFlowObservabilityMetrics.RecordCurrentUserDirectorySyncUpsertDuration(timingSnapshot.OperationName, syncTimings.UpsertElapsedMs);
                    RonFlowObservabilityMetrics.RecordCurrentUserDirectorySyncSaveDuration(timingSnapshot.OperationName, syncTimings.SaveElapsedMs);
                }
            }
        }

        if (stopwatch is not null)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            if (ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
            {
                timingSnapshot!.CurrentUserDirectorySyncElapsedMs = elapsedMs;
                RonFlowObservabilityMetrics.RecordCurrentUserDirectorySyncDuration(timingSnapshot.OperationName, elapsedMs);
            }
        }

        await next(context);
    }
}