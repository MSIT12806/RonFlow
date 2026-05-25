using System.Security.Claims;
using RonFlow.Domain;
using RonFlow.Observability;

namespace RonFlow.Api;

internal sealed class CurrentUserDirectorySyncMiddleware(RequestDelegate next)
{
    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, IUserDirectory userDirectory)
    {
        System.Diagnostics.Stopwatch? stopwatch = null;
        if (BoardReadObservabilityContext.TryGetCurrent(out _))
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
                userDirectory.Upsert(new KnownUser(userId, userName, email));
            }
        }

        if (stopwatch is not null)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            if (BoardReadObservabilityContext.TryGetCurrent(out var timingSnapshot))
            {
                timingSnapshot!.CurrentUserDirectorySyncElapsedMs = elapsedMs;
            }

            RonFlowObservabilityMetrics.RecordBoardCurrentUserDirectorySyncDuration(elapsedMs);
        }

        await next(context);
    }
}