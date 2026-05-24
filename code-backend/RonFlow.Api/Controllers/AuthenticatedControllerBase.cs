using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RonFlow.Api.Controllers;

public abstract class AuthenticatedControllerBase : ControllerBase
{
    protected bool TryGetCurrentUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out userId);
    }

    protected bool TryGetCurrentUserName(out string userName)
    {
        userName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(userName);
    }

    protected bool TryGetCurrentUserEmail(out string email)
    {
        email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? string.Empty;
        return !string.IsNullOrWhiteSpace(email);
    }

    protected bool TryGetRonFlowSessionId(out string sessionId)
    {
        sessionId = Request.Headers[RonFlowSessionConstants.SessionIdHeaderName].FirstOrDefault()
            ?? User.FindFirstValue(RonFlowSessionConstants.SessionIdClaimType)
            ?? string.Empty;
        return !string.IsNullOrWhiteSpace(sessionId);
    }

    protected static IResult AccessDenied()
    {
        return Results.Json(new { message = "Access Denied" }, statusCode: StatusCodes.Status403Forbidden);
    }
}