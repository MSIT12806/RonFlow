using RonFlow.Infrastructure;

namespace RonFlow.Api;

internal sealed class DatabaseSyncRequestUpdateMiddleware(
    RequestDelegate next,
    ILogger<DatabaseSyncRequestUpdateMiddleware> logger)
{
    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, IDatabaseSyncCoordinator databaseSyncCoordinator)
    {
        try
        {
            databaseSyncCoordinator.RequestPullIfStale($"HTTP {context.Request.Method} {context.Request.Path}");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to queue RonFlow database Git pull refresh from request. Method: {Method}; Path: {Path}",
                context.Request.Method,
                context.Request.Path);
        }

        await next(context);
    }
}
