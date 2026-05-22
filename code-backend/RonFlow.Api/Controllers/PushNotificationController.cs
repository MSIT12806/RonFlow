using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Api.Contracts;
using RonFlow.Application;
using RonFlow.Infrastructure;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/notifications/push")]
[Authorize]
public sealed class PushNotificationController : AuthenticatedControllerBase
{
    [HttpGet("public-key")]
    [ProducesResponseType<PushNotificationPublicKeyResponse>(StatusCodes.Status200OK)]
    public ActionResult<PushNotificationPublicKeyResponse> GetPublicKey([FromServices] PushNotificationConfiguration configuration)
    {
        return Ok(new PushNotificationPublicKeyResponse(configuration.PublicKey));
    }

    [HttpPost("subscriptions")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IResult RegisterSubscription(
        [FromBody] RegisterPushSubscriptionRequest request,
        [FromServices] RegisterPushSubscriptionCommandService commandService)
    {
        var result = commandService.Register(request.Endpoint, request.Keys?.P256dh, request.Keys?.Auth);
        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        return Results.Ok();
    }
}