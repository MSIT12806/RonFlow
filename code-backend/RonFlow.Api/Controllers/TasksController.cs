using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class TasksController : AuthenticatedControllerBase
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
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Create(currentUserId, projectId, request.Title);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.AccessDenied)
        {
            return AccessDenied();
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
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Change(currentUserId, projectId, taskId, request.StateKey ?? string.Empty);

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
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Update(currentUserId, projectId, taskId, request.Title, request.Description, request.DueDate);

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

    [HttpPut("{taskId:guid}/subtasks")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult ReplaceSubtasks(
        Guid projectId,
        Guid taskId,
        [FromBody] ReplaceTaskSubtasksRequest request,
        [FromServices] ReplaceTaskSubtasksCommandService commandService)
    {
        if (request.Items is null)
        {
            return ValidationResults.FromError(new ValidationError("items", "完成條件清單為必填欄位"));
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Replace(
            currentUserId,
            projectId,
            taskId,
            request.Items.Select(item => new TaskSubtaskInput(item.Id, item.Title?.Trim() ?? string.Empty, item.IsChecked, item.Order)).ToArray());

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

    [HttpPost("{taskId:guid}/content-edit-lock")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IResult AcquireContentEditLock(
        Guid projectId,
        Guid taskId,
        [FromServices] GetTaskDetailQueryService queryService,
        [FromServices] TaskContentEditLockService lockService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var currentUserName = TryGetCurrentUserName(out var userName)
            ? userName
            : currentUserId.ToString();

        var sessionId = TryGetRonFlowSessionId(out var currentSessionId)
            ? currentSessionId
            : currentUserId.ToString();

        var result = queryService.Get(currentUserId, projectId, taskId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        if (result.NotFound)
        {
            return Results.NotFound();
        }

        if (!lockService.TryAcquire(currentUserId, currentUserName, sessionId, taskId))
        {
            return Results.Conflict();
        }

        return Results.Ok(TaskDetailResponse.FromView(result.Resource!, canEnterEdit: true));
    }

    [HttpDelete("{taskId:guid}/content-edit-lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult ReleaseContentEditLock(
        Guid projectId,
        Guid taskId,
        [FromServices] GetTaskDetailQueryService queryService,
        [FromServices] TaskContentEditLockService lockService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.Get(currentUserId, projectId, taskId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        if (result.NotFound)
        {
            return Results.NotFound();
        }

        lockService.ReleaseIfOwned(currentUserId, taskId);
        return Results.NoContent();
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

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Reorder(currentUserId, projectId, taskId, request.TargetTaskId.Value);

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

    [HttpGet("{taskId:guid}")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetTaskDetail(
        Guid projectId,
        Guid taskId,
        [FromServices] GetTaskDetailQueryService queryService,
        [FromServices] TaskContentEditLockService lockService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.Get(currentUserId, projectId, taskId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(TaskDetailResponse.FromView(result.Resource!, lockService.CanEnterEdit(currentUserId, taskId)));
    }
}