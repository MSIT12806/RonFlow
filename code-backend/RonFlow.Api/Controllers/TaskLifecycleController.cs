using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class TaskLifecycleController : AuthenticatedControllerBase
{
    [HttpGet("archived")]
    [ProducesResponseType<LifecycleTaskListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetArchivedTasks(Guid projectId, [FromServices] GetArchivedTasksQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.Get(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound ? Results.NotFound() : Results.Ok(LifecycleTaskListResponse.FromView(result.Resource!));
    }

    [HttpGet("trashed")]
    [ProducesResponseType<LifecycleTaskListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetTrashedTasks(Guid projectId, [FromServices] GetTrashedTasksQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.Get(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound ? Results.NotFound() : Results.Ok(LifecycleTaskListResponse.FromView(result.Resource!));
    }

    [HttpPatch("{taskId:guid}/archive")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult ArchiveTask(
        Guid projectId,
        Guid taskId,
        [FromServices] ArchiveTaskCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Archive(currentUserId, projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        if (result.Conflict)
        {
            return Results.Conflict();
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }

    [HttpPatch("{taskId:guid}/restore-from-archive")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult RestoreArchivedTask(
        Guid projectId,
        Guid taskId,
        [FromServices] RestoreArchivedTaskCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Restore(currentUserId, projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        if (result.Conflict)
        {
            return Results.Conflict();
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }

    [HttpPatch("{taskId:guid}/trash")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult MoveTaskToTrash(
        Guid projectId,
        Guid taskId,
        [FromServices] MoveTaskToTrashCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Move(currentUserId, projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }

    [HttpPatch("{taskId:guid}/restore-from-trash")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult RestoreTrashedTask(
        Guid projectId,
        Guid taskId,
        [FromServices] RestoreTrashedTaskCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Restore(currentUserId, projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }
}