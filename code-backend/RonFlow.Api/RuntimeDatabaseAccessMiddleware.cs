using RonFlow.Infrastructure;

namespace RonFlow.Api;

internal sealed class RuntimeDatabaseAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRuntimeDatabaseAccessGate databaseAccessGate)
    {
        if (context.Request.Path.StartsWithSegments(DatabaseSyncNotificationHub.Route))
        {
            await next(context);
            return;
        }

        using var lease = await databaseAccessGate.EnterReadAsync(context.RequestAborted);
        await next(context);
    }
}
