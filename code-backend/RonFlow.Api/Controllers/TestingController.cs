using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/testing/faults")]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class TestingController : AuthenticatedControllerBase
{
    [HttpPut]
    public IResult ReplaceFaults([FromBody] ReplaceTestHttpFaultsRequest request, [FromServices] ITestHttpFaultStore faultStore)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        faultStore.Replace(currentUserId, request.Items.Select(item => item.ToFault()).ToArray());
        return Results.Ok();
    }

    [HttpDelete]
    public IResult ClearFaults([FromServices] ITestHttpFaultStore faultStore)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        faultStore.Clear(currentUserId);
        return Results.NoContent();
    }
}