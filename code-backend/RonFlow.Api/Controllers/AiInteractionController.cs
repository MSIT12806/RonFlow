using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;
using RonFlow.Domain;
using System.Text;
using System.Text.Json;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public sealed class AiInteractionController : AuthenticatedControllerBase
{
    [HttpGet("bootstrap")]
    [Produces("text/plain")]
    public IResult GetBootstrap()
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return PlainText(AiTextContractFormatter.Bootstrap());
    }

    [HttpGet("capabilities")]
    [Produces("text/plain")]
    public IResult GetCapabilities()
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return PlainText(AiTextContractFormatter.CapabilitiesManifest());
    }

    [HttpGet("workflow-guidance")]
    [Produces("text/plain")]
    public IResult GetWorkflowGuidance()
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return PlainText(AiTextContractFormatter.WorkflowGuidance());
    }

    [HttpGet("session-summary")]
    [Produces("text/plain")]
    public IResult GetSessionSummary(
        [FromServices] GetProjectsQueryService getProjectsQueryService,
        [FromServices] ProjectPresenceRegistry projectPresenceRegistry)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || !TryGetCurrentUserName(out var currentUserName))
        {
            return Results.Unauthorized();
        }

        var availableScopes = getProjectsQueryService.Get(currentUserId).Items
            .Select(item => item.Id)
            .ToArray();
        var activeScope = TryGetRonFlowSessionId(out var sessionId)
            ? projectPresenceRegistry.GetActiveProjectScope(sessionId)
            : null;

        return PlainText(AiTextContractFormatter.SessionSummary(currentUserName, activeScope, availableScopes));
    }

    [HttpGet("projects/summary")]
    [Produces("text/plain")]
    public IResult GetProjectListSummary(
        [FromServices] GetProjectsQueryService getProjectsQueryService,
        [FromServices] GetProjectBoardQueryService getProjectBoardQueryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var projects = getProjectsQueryService.Get(currentUserId);

        return PlainText(AiTextContractFormatter.ProjectListSummary(
            projects,
            projectId => CountOpenTasks(getProjectBoardQueryService.Get(projectId))));
    }

    [HttpGet("projects/{projectId:guid}/board-summary")]
    [Produces("text/plain")]
    public IResult GetProjectBoardSummary(
        Guid projectId,
        [FromServices] IGetProjectBoardQueryService getProjectBoardQueryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = getProjectBoardQueryService.Get(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        if (result.NotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project list summary again and pick an existing project.");
        }

        return PlainText(AiTextContractFormatter.ProjectBoardSummary(result.Resource!));
    }

    [HttpGet("projects/{projectId:guid}/tasks/{taskId:guid}/detail-summary")]
    [Produces("text/plain")]
    public IResult GetTaskDetailSummary(
        Guid projectId,
        Guid taskId,
        [FromServices] GetTaskDetailQueryService getTaskDetailQueryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = getTaskDetailQueryService.Get(currentUserId, projectId, taskId);
        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        if (result.NotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        return PlainText(AiTextContractFormatter.TaskDetailSummary(result.Resource!));
    }

    [HttpPost("active-scope")]
    public IResult ActivateScope(
        [FromBody] AiActiveScopeRequest request,
        [FromServices] ProjectAccessService projectAccessService,
        [FromServices] ProjectPresenceRegistry projectPresenceRegistry)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || !TryGetCurrentUserName(out var currentUserName))
        {
            return Results.Unauthorized();
        }

        if (!TryGetRonFlowSessionId(out var sessionId))
        {
            return ErrorText(StatusCodes.Status400BadRequest, "SessionNotActivated", "Activate a RonFlow session and retry the operation.", "RonFlow session is not activated.");
        }

        if (!request.ProjectId.HasValue)
        {
            return MissingRequiredField("projectId");
        }

        var access = projectAccessService.GetOwnedProject(currentUserId, request.ProjectId.Value);
        if (access.ProjectNotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project list summary again and pick an existing project.");
        }

        if (access.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        projectPresenceRegistry.EnterProject(currentUserId, currentUserName, sessionId, request.ProjectId.Value);
        return Results.NoContent();
    }

    [HttpPost("apply")]
    [Produces("text/plain")]
    public IResult Apply(
        [FromBody] AiApplyRequest request,
        [FromServices] CreateProjectCommandService createProjectCommandService,
        [FromServices] CreateTaskCommandService createTaskCommandService,
        [FromServices] UpdateTaskCommandService updateTaskCommandService,
        [FromServices] ChangeTaskStateCommandService changeTaskStateCommandService,
        [FromServices] ArchiveTaskCommandService archiveTaskCommandService,
        [FromServices] RestoreArchivedTaskCommandService restoreArchivedTaskCommandService,
        [FromServices] MoveTaskToTrashCommandService moveTaskToTrashCommandService,
        [FromServices] RestoreTrashedTaskCommandService restoreTrashedTaskCommandService,
        [FromServices] ITaskRepository taskRepository,
        [FromServices] TaskContentEditLockService taskContentEditLockService,
        [FromServices] ProjectPresenceRegistry projectPresenceRegistry,
        [FromServices] AiAuditRegistry aiAuditRegistry)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || !TryGetCurrentUserName(out var currentUserName))
        {
            return Results.Unauthorized();
        }

        if (!TryGetRonFlowSessionId(out var sessionId))
        {
            return ErrorText(StatusCodes.Status400BadRequest, "SessionNotActivated", "Activate a RonFlow session and retry the operation.", "RonFlow session is not activated.");
        }

        var operation = request.Operation?.Trim();
        if (string.IsNullOrWhiteSpace(operation))
        {
            return MissingRequiredField("operation");
        }

        var targetType = request.TargetType?.Trim();
        if (string.IsNullOrWhiteSpace(targetType))
        {
            return MissingRequiredField("targetType");
        }

        var requiredFields = request.RequiredFields ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var optionalFields = request.OptionalFields ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        return operation switch
        {
            "create_project" => ApplyCreateProject(createProjectCommandService, aiAuditRegistry, currentUserId, currentUserName, targetType, requiredFields),
            "create_task" => ApplyCreateTask(createTaskCommandService, aiAuditRegistry, currentUserName, sessionId, targetType, requiredFields, projectPresenceRegistry, currentUserId),
            "update_task_detail" => ApplyUpdateTaskDetail(updateTaskCommandService, aiAuditRegistry, taskRepository, taskContentEditLockService, currentUserId, currentUserName, sessionId, targetType, requiredFields, optionalFields, projectPresenceRegistry),
            "move_task_state" => ApplyMoveTaskState(changeTaskStateCommandService, aiAuditRegistry, taskRepository, currentUserId, currentUserName, sessionId, targetType, requiredFields, projectPresenceRegistry),
            "archive_task" => ApplyTaskLifecycle(archiveTaskCommandService.Archive, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "archived"),
            "restore_archived_task" => ApplyTaskLifecycle(restoreArchivedTaskCommandService.Restore, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "active"),
            "trash_task" => ApplyTaskLifecycle(moveTaskToTrashCommandService.Move, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "trashed"),
            "restore_trashed_task" => ApplyTaskLifecycle(restoreTrashedTaskCommandService.Restore, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "active"),
            _ => ErrorText(StatusCodes.Status400BadRequest, "ValidationFailed", $"Use a capability listed in the manifest and submit the write request again.", $"Operation `{operation}` is not supported."),
        };
    }

    [HttpGet("audit-entries/{auditEntryId:guid}")]
    [Produces("text/plain")]
    public IResult GetAuditEntry(Guid auditEntryId, [FromServices] AiAuditRegistry aiAuditRegistry)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        var auditEntry = aiAuditRegistry.Get(auditEntryId);
        if (auditEntry is null)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Use the audit_entry_id from a successful apply result.");
        }

        return PlainText(AiTextContractFormatter.AuditEntry(auditEntry));
    }

    private IResult ApplyCreateProject(
        CreateProjectCommandService createProjectCommandService,
        AiAuditRegistry aiAuditRegistry,
        Guid currentUserId,
        string currentUserName,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields)
    {
        if (!TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        var name = GetRequiredString(requiredFields, "name");
        if (name is null)
        {
            return MissingRequiredField("name");
        }

        var result = createProjectCommandService.Create(currentUserId, currentUserName, currentUserEmail, name);
        if (result.ValidationError is not null)
        {
            return ValidationFailed(result.ValidationError.Field, result.ValidationError.Message);
        }

        var project = result.Project!;
        var actualDiff = new[] { $"name: none -> {project.Name}" };
        var auditEntryId = aiAuditRegistry.Record(currentUserName, targetType, project.Id.ToString(), "create_project", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("create_project", targetType, project.Id.ToString(), ["name"], auditEntryId));
    }

    private IResult ApplyCreateTask(
        CreateTaskCommandService createTaskCommandService,
        AiAuditRegistry aiAuditRegistry,
        string currentUserName,
        string sessionId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields,
        ProjectPresenceRegistry projectPresenceRegistry,
        Guid currentUserId)
    {
        var projectId = GetRequiredGuid(requiredFields, "projectId");
        if (!projectId.HasValue)
        {
            return MissingRequiredField("projectId");
        }

        var scopeError = EnsureScope(projectPresenceRegistry, sessionId, projectId.Value);
        if (scopeError is not null)
        {
            return scopeError;
        }

        var title = GetRequiredString(requiredFields, "title");
        if (title is null)
        {
            return MissingRequiredField("title");
        }

        var result = createTaskCommandService.Create(currentUserId, projectId.Value, title);
        if (result.ValidationError is not null)
        {
            return ValidationFailed(result.ValidationError.Field, result.ValidationError.Message);
        }

        if (result.ProjectNotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project list summary again and pick an existing project.");
        }

        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        var task = result.Task!;
        var actualDiff = new[] { $"title: none -> {task.Title}" };
        var auditEntryId = aiAuditRegistry.Record(currentUserName, targetType, task.Id.ToString(), "create_task", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("create_task", targetType, task.Id.ToString(), ["title"], auditEntryId));
    }

    private IResult ApplyUpdateTaskDetail(
        UpdateTaskCommandService updateTaskCommandService,
        AiAuditRegistry aiAuditRegistry,
        ITaskRepository taskRepository,
        TaskContentEditLockService taskContentEditLockService,
        Guid currentUserId,
        string currentUserName,
        string sessionId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields,
        IReadOnlyDictionary<string, JsonElement> optionalFields,
        ProjectPresenceRegistry projectPresenceRegistry)
    {
        var taskId = GetRequiredGuid(requiredFields, "taskId");
        if (!taskId.HasValue)
        {
            return MissingRequiredField("taskId");
        }

        var task = taskRepository.Get(taskId.Value);
        if (task is null)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        var scopeError = EnsureScope(projectPresenceRegistry, sessionId, task.ProjectId);
        if (scopeError is not null)
        {
            return scopeError;
        }

        if (!taskContentEditLockService.TryAcquire(currentUserId, currentUserName, sessionId, taskId.Value))
        {
            return ErrorText(StatusCodes.Status409Conflict, "ConcurrencyConflict", "Read task detail summary again and retry after the edit lock is released.", "The task is currently locked by another editor.");
        }

        try
        {
            var title = GetOptionalString(optionalFields, "title") ?? task.Title;
            var description = GetOptionalString(optionalFields, "description") ?? task.Description;
            var dueDate = GetOptionalDateOnly(optionalFields, "dueDate") ?? task.DueDate;
            var changedFields = GetChangedFields(task, optionalFields);

            var result = updateTaskCommandService.Update(currentUserId, task.ProjectId, taskId.Value, title, description, dueDate);
            if (result.ValidationError is not null)
            {
                return ValidationFailed(result.ValidationError.Field, result.ValidationError.Message);
            }

            if (result.TaskNotFound)
            {
                return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
            }

            if (result.AccessDenied)
            {
                return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
            }

            if (result.Conflict)
            {
                return ErrorText(StatusCodes.Status409Conflict, "ConcurrencyConflict", "Read task detail summary again and retry after the edit lock is released.", "The task could not be updated because the edit lock is not available.");
            }

            var updatedTask = result.Task!;
            var actualDiff = BuildTaskDetailDiff(task, updatedTask, changedFields);
            var auditEntryId = aiAuditRegistry.Record(currentUserName, targetType, updatedTask.Id.ToString(), "update_task_detail", "success", actualDiff);

            return PlainText(AiTextContractFormatter.ApplyResult("update_task_detail", targetType, updatedTask.Id.ToString(), changedFields, auditEntryId));
        }
        finally
        {
            taskContentEditLockService.ReleaseIfOwned(currentUserId, taskId.Value);
        }
    }

    private IResult ApplyMoveTaskState(
        ChangeTaskStateCommandService changeTaskStateCommandService,
        AiAuditRegistry aiAuditRegistry,
        ITaskRepository taskRepository,
        Guid currentUserId,
        string currentUserName,
        string sessionId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields,
        ProjectPresenceRegistry projectPresenceRegistry)
    {
        var taskId = GetRequiredGuid(requiredFields, "taskId");
        if (!taskId.HasValue)
        {
            return MissingRequiredField("taskId");
        }

        var targetStateKey = GetRequiredString(requiredFields, "targetStateKey");
        if (targetStateKey is null)
        {
            return MissingRequiredField("targetStateKey");
        }

        var task = taskRepository.Get(taskId.Value);
        if (task is null)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        var scopeError = EnsureScope(projectPresenceRegistry, sessionId, task.ProjectId);
        if (scopeError is not null)
        {
            return scopeError;
        }

        var beforeStateKey = task.CurrentState.Key;
        var result = changeTaskStateCommandService.Change(currentUserId, task.ProjectId, taskId.Value, targetStateKey);
        if (result.ValidationError is not null)
        {
            return ValidationFailed(result.ValidationError.Field, result.ValidationError.Message);
        }

        if (result.TaskNotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        var changedTask = result.Task!;
        var actualDiff = new[] { $"workflow_state_key: {NormalizeText(beforeStateKey)} -> {NormalizeText(changedTask.CurrentState.Key)}" };
        var auditEntryId = aiAuditRegistry.Record(currentUserName, targetType, changedTask.Id.ToString(), "move_task_state", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("move_task_state", targetType, changedTask.Id.ToString(), ["workflow_state_key"], auditEntryId));
    }

    private IResult ApplyTaskLifecycle(
        Func<Guid, Guid, Guid, TaskLifecycleCommandResult> operationHandler,
        AiAuditRegistry aiAuditRegistry,
        ITaskRepository taskRepository,
        string currentUserName,
        string sessionId,
        Guid currentUserId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields,
        ProjectPresenceRegistry projectPresenceRegistry,
        string operation,
        string changedField,
        string changedTo)
    {
        var taskId = GetRequiredGuid(requiredFields, "taskId");
        if (!taskId.HasValue)
        {
            return MissingRequiredField("taskId");
        }

        var task = taskRepository.Get(taskId.Value);
        if (task is null)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        var scopeError = EnsureScope(projectPresenceRegistry, sessionId, task.ProjectId);
        if (scopeError is not null)
        {
            return scopeError;
        }

        var result = operationHandler(currentUserId, task.ProjectId, taskId.Value);
        if (result.ValidationError is not null)
        {
            return ValidationFailed(result.ValidationError.Field, result.ValidationError.Message);
        }

        if (result.TaskNotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        if (result.Conflict)
        {
            return ErrorText(StatusCodes.Status409Conflict, "ConcurrencyConflict", "Read task detail summary again and retry after the conflicting operation finishes.", "The task is currently locked by another editor.");
        }

        var changedTask = result.Task!;
        var actualDiff = new[] { $"{changedField}: {NormalizeText(task.LifecycleState.ToString())} -> {changedTo}" };
        var auditEntryId = aiAuditRegistry.Record(currentUserName, targetType, changedTask.Id.ToString(), operation, "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult(operation, targetType, changedTask.Id.ToString(), [changedField], auditEntryId));
    }

    private static IResult? EnsureScope(ProjectPresenceRegistry projectPresenceRegistry, string sessionId, Guid expectedProjectId)
    {
        var activeScope = projectPresenceRegistry.GetActiveProjectScope(sessionId);
        if (activeScope == expectedProjectId)
        {
            return null;
        }

        return ErrorText(StatusCodes.Status400BadRequest, "ScopeRequired", "Activate the correct project scope and submit the write request again.", "The requested operation requires an active project scope.");
    }

    private static IResult MissingRequiredField(string fieldName)
    {
        return ErrorText(StatusCodes.Status400BadRequest, "ValidationFailed", $"Provide `{fieldName}` and submit the write request again.", $"Required field `{fieldName}` is missing.");
    }

    private static IResult ValidationFailed(string fieldName, string message)
    {
        return ErrorText(StatusCodes.Status400BadRequest, "ValidationFailed", $"Correct `{fieldName}` and submit the write request again.", message);
    }

    private static string? GetRequiredString(IReadOnlyDictionary<string, JsonElement> fields, string fieldName)
    {
        var value = GetOptionalString(fields, fieldName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetOptionalString(IReadOnlyDictionary<string, JsonElement> fields, string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static Guid? GetRequiredGuid(IReadOnlyDictionary<string, JsonElement> fields, string fieldName)
    {
        var rawValue = GetOptionalString(fields, fieldName);
        return Guid.TryParse(rawValue, out var parsedGuid)
            ? parsedGuid
            : null;
    }

    private static DateOnly? GetOptionalDateOnly(IReadOnlyDictionary<string, JsonElement> fields, string fieldName)
    {
        var rawValue = GetOptionalString(fields, fieldName);
        return DateOnly.TryParse(rawValue, out var parsedDate)
            ? parsedDate
            : null;
    }

    private static IReadOnlyList<string> GetChangedFields(RonFlow.Domain.Task task, IReadOnlyDictionary<string, JsonElement> optionalFields)
    {
        var changedFields = new List<string>();

        if (optionalFields.ContainsKey("title"))
        {
            changedFields.Add("title");
        }

        if (optionalFields.ContainsKey("description"))
        {
            changedFields.Add("description");
        }

        if (optionalFields.ContainsKey("dueDate"))
        {
            changedFields.Add("dueDate");
        }

        return changedFields.Count == 0 ? ["title"] : changedFields;
    }

    private static IReadOnlyList<string> BuildTaskDetailDiff(RonFlow.Domain.Task task, CreateTaskOutput updatedTask, IReadOnlyList<string> changedFields)
    {
        var diff = new List<string>();

        foreach (var changedField in changedFields)
        {
            switch (changedField)
            {
                case "title":
                    diff.Add($"title: {NormalizeText(task.Title)} -> {NormalizeText(updatedTask.Title)}");
                    break;
                case "description":
                    diff.Add($"description: {NormalizeText(task.Description)} -> {NormalizeText(updatedTask.Description)}");
                    break;
                case "dueDate":
                    diff.Add($"dueDate: {(task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy-MM-dd") : "none")} -> {(updatedTask.DueDate.HasValue ? updatedTask.DueDate.Value.ToString("yyyy-MM-dd") : "none")}");
                    break;
            }
        }

        return diff;
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : value;
    }

    private static int CountOpenTasks(ProjectBoardView? board)
    {
        return board?.Columns.Sum(column => column.Tasks.Count) ?? 0;
    }

    private static IResult PlainText(string content)
    {
        return Results.Text(content, "text/plain; charset=utf-8");
    }

    private static IResult ErrorText(int statusCode, string errorCode, string recoveryHint, string? explicitMessage = null)
    {
        var message = explicitMessage ?? errorCode switch
        {
            "Forbidden" => "Access to the requested resource is denied.",
            "ResourceNotFound" => "The requested resource does not exist.",
            "ScopeRequired" => "The requested operation requires an active project scope.",
            "SessionNotActivated" => "RonFlow session is not activated.",
            _ => errorCode,
        };

        return Results.Text(
            AiTextContractFormatter.Error(errorCode, message, recoveryHint),
            "text/plain; charset=utf-8",
            Encoding.UTF8,
            statusCode);
    }
}