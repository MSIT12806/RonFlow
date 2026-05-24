using System.Security.Claims;
using RonFlow.Application;

namespace RonFlow.Api;

public sealed class RonFlowActiveSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RonFlowActiveSessionRegistry activeSessionRegistry)
    {
        if (context.User.Identity?.IsAuthenticated != true || IsSessionActivationRequest(context.Request.Path))
        {
            await next(context);
            return;
        }

        var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        var sessionId = context.Request.Headers[RonFlowSessionConstants.SessionIdHeaderName].FirstOrDefault()
            ?? context.User.FindFirstValue(RonFlowSessionConstants.SessionIdClaimType);

        if (!Guid.TryParse(rawUserId, out var userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            await next(context);
            return;
        }

        if (!activeSessionRegistry.IsActive(userId, sessionId))
        {
            context.Response.Headers[RonFlowSessionConstants.SessionInvalidatedHeaderName] = "true";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "RonFlow session invalidated" });
            return;
        }

        await next(context);
    }

    private static bool IsSessionActivationRequest(PathString path)
    {
        return string.Equals(path.Value, "/api/session/activate", StringComparison.OrdinalIgnoreCase);
    }
}