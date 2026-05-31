using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;
using RonFlow.Domain;

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
        [FromBody] JsonDocument requestBody,
        [FromServices] UpdateTaskCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var request = requestBody.RootElement;
        var title = request.TryGetProperty("title", out var titleElement) && titleElement.ValueKind != JsonValueKind.Null
            ? titleElement.GetString()
            : null;
        var description = request.TryGetProperty("description", out var descriptionElement) && descriptionElement.ValueKind != JsonValueKind.Null
            ? descriptionElement.GetString()
            : null;
        var dueDate = request.TryGetProperty("dueDate", out var dueDateElement) && dueDateElement.ValueKind != JsonValueKind.Null
            ? DateOnly.Parse(dueDateElement.GetString() ?? string.Empty)
            : (DateOnly?)null;
        var rawCodeTraceability = request.TryGetProperty("codeTraceability", out var codeTraceabilityElement)
            ? codeTraceabilityElement
            : (JsonElement?)null;

        if (!TryMapCodeTraceability(rawCodeTraceability, out var codeTraceability, out var validationError))
        {
            return ValidationResults.FromError(validationError!);
        }

        var result = commandService.Update(currentUserId, projectId, taskId, title, description, dueDate, codeTraceability);

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

    private static bool TryMapCodeTraceability(
        JsonElement? request,
        out TaskCodeTraceability? codeTraceability,
        out ValidationError? validationError)
    {
        codeTraceability = null;
        validationError = null;

        if (request is null || request.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (request.Value.ValueKind != JsonValueKind.Object)
        {
            validationError = new ValidationError("codeTraceability", "程式修改追蹤必須是物件");
            return false;
        }

        if (!TryMapCodeTraceabilityItems(request.Value, "api", "codeTraceability.api", out var apiItems, out validationError)
            || !TryMapCodeTraceabilityItems(request.Value, "frontendPages", "codeTraceability.frontendPages", out var frontendPageItems, out validationError)
            || !TryMapCodeTraceabilityItems(request.Value, "frontendComponents", "codeTraceability.frontendComponents", out var frontendComponentItems, out validationError))
        {
            return false;
        }

        codeTraceability = new TaskCodeTraceability(apiItems, frontendPageItems, frontendComponentItems);
        return true;
    }

    private static bool TryMapCodeTraceabilityItems(
        JsonElement request,
        string propertyName,
        string fieldPrefix,
        out IReadOnlyList<TaskCodeTraceabilityItem> mappedItems,
        out ValidationError? validationError)
    {
        mappedItems = [];
        validationError = null;

        if (!request.TryGetProperty(propertyName, out var itemsElement) || itemsElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (itemsElement.ValueKind != JsonValueKind.Array)
        {
            validationError = new ValidationError(fieldPrefix, "程式修改追蹤清單必須是陣列");
            return false;
        }

        var results = new List<TaskCodeTraceabilityItem>();
        var index = 0;

        foreach (var item in itemsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                validationError = new ValidationError($"{fieldPrefix}[{index}]", "程式修改追蹤項目必須是物件");
                return false;
            }

            var changeType = item.TryGetProperty("changeType", out var changeTypeElement) && changeTypeElement.ValueKind != JsonValueKind.Null
                ? (changeTypeElement.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;

            if (changeType is not ("added" or "modified" or "removed"))
            {
                validationError = new ValidationError($"{fieldPrefix}[{index}].changeType", "程式修改類型必須為 added、modified 或 removed");
                return false;
            }

            var target = item.TryGetProperty("target", out var targetElement) && targetElement.ValueKind != JsonValueKind.Null
                ? (targetElement.GetString() ?? string.Empty).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(target))
            {
                validationError = new ValidationError($"{fieldPrefix}[{index}].target", "程式修改項目名稱為必填欄位");
                return false;
            }

            results.Add(new TaskCodeTraceabilityItem(changeType, target));
            index += 1;
        }

        mappedItems = results;
        return true;
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