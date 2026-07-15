using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Api.Contracts;
using RonFlow.Infrastructure;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/notifications/database-sync")]
[Authorize]
public sealed class DatabaseSyncNotificationsController : AuthenticatedControllerBase
{
    [HttpGet]
    [ProducesResponseType<DatabaseSyncOperationListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IResult List([FromServices] IDatabaseSyncOperationStore operationStore, [FromQuery] int limit = 20)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return AccessDenied();
        }

        var operations = operationStore
            .GetForInitiator(userId, limit)
            .Select(DatabaseSyncOperationResponse.FromOperation)
            .ToArray();

        return Results.Ok(new DatabaseSyncOperationListResponse(operations));
    }
}
