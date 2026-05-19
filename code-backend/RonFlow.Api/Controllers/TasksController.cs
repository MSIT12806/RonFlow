using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
public sealed class TasksController : ControllerBase
{
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult CreateTask(
        Guid projectId,
        [FromBody] CreateTaskRequest request,
        [FromServices] CreateTaskCommandService commandService)
    {
        var result = commandService.Create(projectId, request.Title);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.ProjectNotFound)
        {
            return Results.NotFound();
        }

        var task = result.Task!;

        return Results.Created($"/api/projects/{projectId}/tasks/{task.Id}", TaskDetailResponse.FromOutput(task));
    }

    [HttpPatch("{taskId:guid}/state")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult ChangeTaskState(
        Guid projectId,
        Guid taskId,
        [FromBody] ChangeTaskStateRequest request,
        [FromServices] ChangeTaskStateCommandService commandService)
    {
        var result = commandService.Change(projectId, taskId, request.StateKey ?? string.Empty);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }

    [HttpPatch("{taskId:guid}")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult UpdateTask(
        Guid projectId,
        Guid taskId,
        [FromBody] UpdateTaskRequest request,
        [FromServices] UpdateTaskCommandService commandService)
    {
        var result = commandService.Update(projectId, taskId, request.Title, request.Description, request.DueDate);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }

    [HttpPatch("{taskId:guid}/order")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult ReorderTask(
        Guid projectId,
        Guid taskId,
        [FromBody] ReorderTaskRequest request,
        [FromServices] ReorderTaskCommandService commandService)
    {
        if (request.TargetTaskId is null)
        {
            return ValidationResults.FromError(new ValidationError("targetTaskId", "目標任務為必填欄位"));
        }

        var result = commandService.Reorder(projectId, taskId, request.TargetTaskId.Value);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        return result.TaskNotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }

    [HttpGet("{taskId:guid}")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetTaskDetail(
        Guid projectId,
        Guid taskId,
        [FromServices] GetTaskDetailQueryService queryService)
    {
        var task = queryService.Get(projectId, taskId);
        return task is null ? Results.NotFound() : Results.Ok(TaskDetailResponse.FromView(task));
    }
}