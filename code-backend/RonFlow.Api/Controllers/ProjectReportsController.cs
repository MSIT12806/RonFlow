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

    [HttpGet("task-aging")]
    [ProducesResponseType<TaskAgingReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetTaskAging(
        Guid projectId,
        [FromQuery] int? todoThresholdDays,
        [FromQuery] int? activeThresholdDays,
        [FromQuery] int? reviewThresholdDays,
        [FromServices] GetTaskAgingReportQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var validationError = ValidateThreshold(todoThresholdDays, "todoThresholdDays")
            ?? ValidateThreshold(activeThresholdDays, "activeThresholdDays")
            ?? ValidateThreshold(reviewThresholdDays, "reviewThresholdDays");
        if (validationError is not null)
        {
            return ValidationResults.FromError(validationError);
        }

        var result = queryService.Get(
            currentUserId,
            projectId,
            new TaskAgingThresholdOverrides(todoThresholdDays, activeThresholdDays, reviewThresholdDays));

        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(TaskAgingReportResponse.FromView(result.Resource!));
    }

    private static ValidationError? ValidateThreshold(int? value, string fieldName)
    {
        if (value is not null && value.Value < 0)
        {
            return new ValidationError(fieldName, "停留閾值必須為 0 或正整數");
        }

        return null;
    }

    [HttpGet("cycle-time")]
    [ProducesResponseType<CycleTimeReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetCycleTime(
        Guid projectId,
        [FromQuery] DateOnly? completedFrom,
        [FromQuery] DateOnly? completedTo,
        [FromServices] GetCycleTimeReportQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        if (completedFrom is not null && completedTo is not null && completedFrom > completedTo)
        {
            return ValidationResults.FromError(new ValidationError("completedFrom", "completedFrom 不可晚於 completedTo"));
        }

        var result = queryService.Get(currentUserId, projectId, completedFrom, completedTo);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(CycleTimeReportResponse.FromView(result.Resource!));
    }
}