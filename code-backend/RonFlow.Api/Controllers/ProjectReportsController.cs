using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Api.Contracts;
using RonFlow.Application;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/reports")]
[Authorize]
public sealed class ProjectReportsController : AuthenticatedControllerBase
{
    [HttpGet("workflow-throughput")]
    [ProducesResponseType<WorkflowThroughputReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetWorkflowThroughput(
        Guid projectId,
        [FromQuery] string? bucket,
        [FromServices] GetWorkflowThroughputReportQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        if (!ReportingBucketTypeParser.TryParse(bucket ?? "day", out var bucketType))
        {
            return ValidationResults.FromError(new ValidationError("bucket", "統計粒度必須為 day 或 week"));
        }

        var result = queryService.Get(currentUserId, projectId, bucketType);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(WorkflowThroughputReportResponse.FromView(result.Resource!));
    }
}