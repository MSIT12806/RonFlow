using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/session")]
[Authorize]
public sealed class SessionController : AuthenticatedControllerBase
{
    [HttpPost("activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IResult Activate([FromServices] RonFlowActiveSessionRegistry activeSessionRegistry)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        if (!TryGetRonFlowSessionId(out var sessionId))
        {
            return Results.BadRequest();
        }

        activeSessionRegistry.Activate(currentUserId, sessionId);
        return Results.NoContent();
    }

    [HttpPost("project-scope/release")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IResult ReleaseProjectScope([FromServices] RonFlowActiveSessionRegistry activeSessionRegistry)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        if (!TryGetRonFlowSessionId(out var sessionId))
        {
            return Results.BadRequest();
        }

        activeSessionRegistry.ReleaseProjectScope(sessionId);
        return Results.NoContent();
    }
}