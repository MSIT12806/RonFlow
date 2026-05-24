using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/invitations")]
[Authorize]
public sealed class InvitationsController : AuthenticatedControllerBase
{
    [HttpGet]
    [ProducesResponseType<InvitationInboxResponse>(StatusCodes.Status200OK)]
    public IResult GetInbox([FromServices] ProjectCollaborationQueryService queryService)
    {
        if (!TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(InvitationInboxResponse.FromView(queryService.GetInvitationInbox(currentUserEmail)));
    }

    [HttpPost("{invitationId:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult Accept(Guid invitationId, [FromServices] ProjectInvitationCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId)
            || !TryGetCurrentUserName(out var currentUserName)
            || !TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Accept(currentUserId, currentUserName, currentUserEmail, invitationId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        if (result.AlreadyHandled)
        {
            return Results.Json(new { message = "Invitation already handled" }, statusCode: StatusCodes.Status409Conflict);
        }

        return result.InvitationNotFound ? Results.NotFound() : Results.Ok();
    }

    [HttpPost("{invitationId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult Reject(Guid invitationId, [FromServices] ProjectInvitationCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId)
            || !TryGetCurrentUserName(out var currentUserName)
            || !TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Reject(currentUserId, currentUserName, currentUserEmail, invitationId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.InvitationNotFound ? Results.NotFound() : Results.Ok();
    }
}