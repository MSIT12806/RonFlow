using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
public sealed class TaskLifecycleController : ControllerBase
{
    [HttpGet("archived")]
    [ProducesResponseType<LifecycleTaskListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetArchivedTasks(Guid projectId, [FromServices] GetArchivedTasksQueryService queryService)
    {
        var tasks = queryService.Get(projectId);
        return tasks is null ? Results.NotFound() : Results.Ok(LifecycleTaskListResponse.FromView(tasks));
    }

    [HttpGet("trashed")]
    [ProducesResponseType<LifecycleTaskListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetTrashedTasks(Guid projectId, [FromServices] GetTrashedTasksQueryService queryService)
    {
        var tasks = queryService.Get(projectId);
        return tasks is null ? Results.NotFound() : Results.Ok(LifecycleTaskListResponse.FromView(tasks));
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
        var result = commandService.Archive(projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
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
        var result = commandService.Restore(projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
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
        var result = commandService.Move(projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
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
        var result = commandService.Restore(projectId, taskId);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }
}