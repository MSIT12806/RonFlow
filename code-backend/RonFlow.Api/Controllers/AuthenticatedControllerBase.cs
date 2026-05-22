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

    protected static IResult AccessDenied()
    {
        return Results.Json(new { message = "Access Denied" }, statusCode: StatusCodes.Status403Forbidden);
    }
}