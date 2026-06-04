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
    [Produces("text/plain", "application/json")]
    public IResult GetBootstrap([FromQuery] string? format = null)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return ContractResponse(AiTextContractFormatter.Bootstrap(), AiJsonContractFormatter.Bootstrap(), format);
    }

    [HttpGet("glossary")]
    [Produces("text/plain", "application/json")]
    public IResult GetGlossary([FromQuery] string? format = null)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return ContractResponse(AiTextContractFormatter.Glossary(), AiJsonContractFormatter.Glossary(), format);
    }

    [HttpGet("capabilities")]
    [Produces("text/plain", "application/json")]
    public IResult GetCapabilities([FromQuery] string? format = null)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return ContractResponse(AiTextContractFormatter.CapabilitiesManifest(), AiJsonContractFormatter.CapabilitiesManifest(), format);
    }

    [HttpGet("workflow-guidance")]
    [Produces("text/plain", "application/json")]
    public IResult GetWorkflowGuidance([FromQuery] string? format = null)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return ContractResponse(AiTextContractFormatter.WorkflowGuidance(), AiJsonContractFormatter.WorkflowGuidance(), format);
    }

    [HttpGet("session-summary")]
    [Produces("text/plain", "application/json")]
    public IResult GetSessionSummary(
        [FromQuery] string? format,
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

        return ContractResponse(
            AiTextContractFormatter.SessionSummary(currentUserName, activeScope, availableScopes),
            AiJsonContractFormatter.SessionSummary(currentUserName, activeScope, availableScopes),
            format);
    }

    [HttpGet("projects/summary")]
    [Produces("text/plain", "application/json")]
    public IResult GetProjectListSummary(
        [FromQuery] string? format,
        [FromServices] GetProjectsQueryService getProjectsQueryService,
        [FromServices] GetProjectBoardQueryService getProjectBoardQueryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var projects = getProjectsQueryService.Get(currentUserId);

        int OpenTaskCount(Guid projectId) => CountOpenTasks(getProjectBoardQueryService.Get(projectId));

        return ContractResponse(
            AiTextContractFormatter.ProjectListSummary(projects, OpenTaskCount),
            AiJsonContractFormatter.ProjectListSummary(projects, OpenTaskCount),
            format);
    }

    [HttpGet("invitations/summary")]
    [Produces("text/plain", "application/json")]
    public IResult GetInvitationInboxSummary(
        [FromQuery] string? format,
        [FromServices] ProjectCollaborationQueryService queryService)
    {
        if (!TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        var inbox = queryService.GetInvitationInbox(currentUserEmail);

        return ContractResponse(
            AiTextContractFormatter.InvitationInboxSummary(inbox),
            AiJsonContractFormatter.InvitationInboxSummary(inbox),
            format);
    }

    [HttpGet("projects/{projectId:guid}/board-summary")]
    [Produces("text/plain", "application/json")]
    public IResult GetProjectBoardSummary(
        Guid projectId,
        [FromQuery] string? format,
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

        return ContractResponse(
            AiTextContractFormatter.ProjectBoardSummary(result.Resource!),
            AiJsonContractFormatter.ProjectBoardSummary(result.Resource!),
            format);
    }

    [HttpGet("projects/{projectId:guid}/current-work-summary")]
    [Produces("text/plain", "application/json")]
    public IResult GetCurrentWorkSummary(
        Guid projectId,
        [FromQuery] string? format,
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

        return ContractResponse(
            AiTextContractFormatter.CurrentWorkSummary(result.Resource!),
            AiJsonContractFormatter.CurrentWorkSummary(result.Resource!),
            format);
    }

    [HttpGet("projects/{projectId:guid}/tasks/{taskId:guid}/detail-summary")]
    [Produces("text/plain", "application/json")]
    public IResult GetTaskDetailSummary(
        Guid projectId,
        Guid taskId,
        [FromQuery] string? format,
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

        return ContractResponse(
            AiTextContractFormatter.TaskDetailSummary(result.Resource!),
            AiJsonContractFormatter.TaskDetailSummary(result.Resource!),
            format);
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
        [FromServices] ReorderTaskCommandService reorderTaskCommandService,
        [FromServices] ReplaceTaskSubtasksCommandService replaceTaskSubtasksCommandService,
        [FromServices] ArchiveTaskCommandService archiveTaskCommandService,
        [FromServices] RestoreArchivedTaskCommandService restoreArchivedTaskCommandService,
        [FromServices] MoveTaskToTrashCommandService moveTaskToTrashCommandService,
        [FromServices] RestoreTrashedTaskCommandService restoreTrashedTaskCommandService,
        [FromServices] ProjectInvitationCommandService projectInvitationCommandService,
        [FromServices] ProjectCollaborationQueryService projectCollaborationQueryService,
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
            "create_project" => ApplyCreateProject(createProjectCommandService, aiAuditRegistry, currentUserId, currentUserName, sessionId, targetType, requiredFields),
            "create_task" => ApplyCreateTask(createTaskCommandService, aiAuditRegistry, currentUserName, sessionId, targetType, requiredFields, projectPresenceRegistry, currentUserId),
            "invite_project_member" => ApplyInviteProjectMember(projectInvitationCommandService, aiAuditRegistry, currentUserName, sessionId, targetType, requiredFields, projectPresenceRegistry),
            "accept_project_invitation" => ApplyAcceptProjectInvitation(projectInvitationCommandService, projectCollaborationQueryService, aiAuditRegistry, currentUserId, currentUserName, sessionId, targetType, requiredFields),
            "reject_project_invitation" => ApplyRejectProjectInvitation(projectInvitationCommandService, projectCollaborationQueryService, aiAuditRegistry, currentUserId, currentUserName, sessionId, targetType, requiredFields),
            "update_task_detail" => ApplyUpdateTaskDetail(updateTaskCommandService, aiAuditRegistry, taskRepository, taskContentEditLockService, currentUserId, currentUserName, sessionId, targetType, requiredFields, optionalFields, projectPresenceRegistry),
            "check_task_subtask" => ApplyToggleTaskSubtask(replaceTaskSubtasksCommandService, aiAuditRegistry, taskRepository, currentUserId, currentUserName, sessionId, targetType, requiredFields, projectPresenceRegistry, operation, true),
            "uncheck_task_subtask" => ApplyToggleTaskSubtask(replaceTaskSubtasksCommandService, aiAuditRegistry, taskRepository, currentUserId, currentUserName, sessionId, targetType, requiredFields, projectPresenceRegistry, operation, false),
            "move_task_state" => ApplyMoveTaskState(changeTaskStateCommandService, aiAuditRegistry, taskRepository, currentUserId, currentUserName, sessionId, targetType, requiredFields, projectPresenceRegistry),
            "reorder_task" => ApplyReorderTask(reorderTaskCommandService, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry),
            "archive_task" => ApplyTaskLifecycle(archiveTaskCommandService.Archive, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "archived"),
            "restore_archived_task" => ApplyTaskLifecycle(restoreArchivedTaskCommandService.Restore, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "active"),
            "trash_task" => ApplyTaskLifecycle(moveTaskToTrashCommandService.Move, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "trashed"),
            "restore_trashed_task" => ApplyTaskLifecycle(restoreTrashedTaskCommandService.Restore, aiAuditRegistry, taskRepository, currentUserName, sessionId, currentUserId, targetType, requiredFields, projectPresenceRegistry, operation, "lifecycle_state", "active"),
            _ => ErrorText(StatusCodes.Status400BadRequest, "ValidationFailed", $"Use a capability listed in the manifest and submit the write request again.", $"Operation `{operation}` is not supported."),
        };
    }

    [HttpGet("audit-entries")]
    [Produces("text/plain")]
    public IResult GetAuditEntries(
        [FromQuery] string? sessionId,
        [FromQuery] string? actorIdentity,
        [FromQuery] string? targetType,
        [FromQuery] string? targetId,
        [FromQuery] string? requestedChange,
        [FromQuery] string? actualDiffContains,
        [FromQuery] int? limit,
        [FromServices] AiAuditRegistry aiAuditRegistry)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        var query = new AiAuditQuery(
            sessionId,
            actorIdentity,
            targetType,
            targetId,
            requestedChange,
            actualDiffContains,
            limit ?? 20);

        var entries = aiAuditRegistry.Query(query);
        return PlainText(AiTextContractFormatter.AuditEntriesSummary(query, entries));
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
        string sessionId,
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
            return MissingApplyRequiredField("name");
        }

        var result = createProjectCommandService.Create(currentUserId, currentUserName, currentUserEmail, name);
        if (result.ValidationError is not null)
        {
            return ValidationFailed(result.ValidationError.Field, result.ValidationError.Message);
        }

        var project = result.Project!;
        var actualDiff = new[] { $"name: none -> {project.Name}" };
        var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, project.Id.ToString(), "create_project", "success", actualDiff);

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
            return MissingApplyRequiredField("projectId");
        }

        var scopeError = EnsureScope(projectPresenceRegistry, sessionId, projectId.Value);
        if (scopeError is not null)
        {
            return scopeError;
        }

        var title = GetRequiredString(requiredFields, "title");
        if (title is null)
        {
            return MissingApplyRequiredField("title");
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
        var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, task.Id.ToString(), "create_task", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("create_task", targetType, task.Id.ToString(), ["title"], auditEntryId));
    }

    private IResult ApplyInviteProjectMember(
        ProjectInvitationCommandService projectInvitationCommandService,
        AiAuditRegistry aiAuditRegistry,
        string currentUserName,
        string sessionId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields,
        ProjectPresenceRegistry projectPresenceRegistry)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var projectId = GetRequiredGuid(requiredFields, "projectId");
        if (!projectId.HasValue)
        {
            return MissingApplyRequiredField("projectId");
        }

        var scopeError = EnsureScope(projectPresenceRegistry, sessionId, projectId.Value);
        if (scopeError is not null)
        {
            return scopeError;
        }

        var invitee = GetRequiredString(requiredFields, "invitee");
        if (invitee is null)
        {
            return MissingApplyRequiredField("invitee");
        }

        var result = projectInvitationCommandService.Invite(currentUserId, currentUserName, projectId.Value, invitee);
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

        var invitation = result.Invitation!;
        var actualDiff = new[] { $"invitation:{invitation.Invitee}: none -> {invitation.Status}" };
        var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, projectId.Value.ToString(), "invite_project_member", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("invite_project_member", targetType, projectId.Value.ToString(), ["invitations"], auditEntryId));
    }

    private IResult ApplyAcceptProjectInvitation(
        ProjectInvitationCommandService projectInvitationCommandService,
        ProjectCollaborationQueryService projectCollaborationQueryService,
        AiAuditRegistry aiAuditRegistry,
        Guid currentUserId,
        string currentUserName,
        string sessionId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields)
    {
        if (!TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        var invitationId = GetRequiredGuid(requiredFields, "invitationId");
        if (!invitationId.HasValue)
        {
            return MissingApplyRequiredField("invitationId");
        }

        var pendingInvitation = projectCollaborationQueryService.GetInvitationInbox(currentUserEmail)
            .Items
            .SingleOrDefault(item => item.Id == invitationId.Value);
        var result = projectInvitationCommandService.Accept(currentUserId, currentUserName, currentUserEmail, invitationId.Value);
        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Read invitation inbox summary again and pick an invitation addressed to the current actor.");
        }

        if (result.InvitationNotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read invitation inbox summary again and pick an existing invitation.");
        }

        if (result.AlreadyHandled)
        {
            return ErrorText(StatusCodes.Status409Conflict, "ConcurrencyConflict", "Read invitation inbox summary again and pick a pending invitation.", "The invitation has already been handled.");
        }

        var membershipTarget = pendingInvitation is null
            ? "project member"
            : $"member of {pendingInvitation.ProjectName} ({pendingInvitation.ProjectId})";
        var actualDiff = new[]
        {
            $"membership: none -> {membershipTarget}",
            "invitation_status: Pending -> Accepted",
        };
        var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, invitationId.Value.ToString(), "accept_project_invitation", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("accept_project_invitation", targetType, invitationId.Value.ToString(), ["membership", "invitation_status"], auditEntryId));
    }

    private IResult ApplyRejectProjectInvitation(
        ProjectInvitationCommandService projectInvitationCommandService,
        ProjectCollaborationQueryService projectCollaborationQueryService,
        AiAuditRegistry aiAuditRegistry,
        Guid currentUserId,
        string currentUserName,
        string sessionId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields)
    {
        if (!TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        var invitationId = GetRequiredGuid(requiredFields, "invitationId");
        if (!invitationId.HasValue)
        {
            return MissingApplyRequiredField("invitationId");
        }

        var pendingInvitation = projectCollaborationQueryService.GetInvitationInbox(currentUserEmail)
            .Items
            .SingleOrDefault(item => item.Id == invitationId.Value);
        var result = projectInvitationCommandService.Reject(currentUserId, currentUserName, currentUserEmail, invitationId.Value);
        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Read invitation inbox summary again and pick an invitation addressed to the current actor.");
        }

        if (result.InvitationNotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read invitation inbox summary again and pick a pending invitation.");
        }

        var projectName = pendingInvitation?.ProjectName ?? "project";
        var actualDiff = new[] { $"invitation_status:{projectName}: Pending -> Rejected" };
        var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, invitationId.Value.ToString(), "reject_project_invitation", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("reject_project_invitation", targetType, invitationId.Value.ToString(), ["invitation_status"], auditEntryId));
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
            return MissingApplyRequiredField("taskId");
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
            var codeTraceability = GetOptionalCodeTraceability(optionalFields, out var validationError);
            if (validationError is not null)
            {
                return ValidationFailed($"optionalFields.{validationError.Field}", validationError.Message);
            }

            var changedFields = GetChangedFields(task, optionalFields);
            var beforeTaskDetail = TaskDetailSnapshot.FromTask(task);

            var result = updateTaskCommandService.Update(currentUserId, task.ProjectId, taskId.Value, title, description, dueDate, codeTraceability);
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
            var actualDiff = BuildTaskDetailDiff(beforeTaskDetail, updatedTask, changedFields);
            var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, updatedTask.Id.ToString(), "update_task_detail", "success", actualDiff);

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
            return MissingApplyRequiredField("taskId");
        }

        var targetStateKey = GetRequiredString(requiredFields, "targetStateKey");
        if (targetStateKey is null)
        {
            return MissingApplyRequiredField("targetStateKey");
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
        var normalizedTargetStateKey = NormalizeWorkflowStateKeyForCommand(targetStateKey);
        var result = changeTaskStateCommandService.Change(currentUserId, task.ProjectId, taskId.Value, normalizedTargetStateKey);
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
    var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, changedTask.Id.ToString(), "move_task_state", "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult("move_task_state", targetType, changedTask.Id.ToString(), ["workflow_state_key"], auditEntryId));
    }

    private IResult ApplyToggleTaskSubtask(
        ReplaceTaskSubtasksCommandService replaceTaskSubtasksCommandService,
        AiAuditRegistry aiAuditRegistry,
        ITaskRepository taskRepository,
        Guid currentUserId,
        string currentUserName,
        string sessionId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields,
        ProjectPresenceRegistry projectPresenceRegistry,
        string operation,
        bool isChecked)
    {
        var taskId = GetRequiredGuid(requiredFields, "taskId");
        if (!taskId.HasValue)
        {
            return MissingApplyRequiredField("taskId");
        }

        var subtaskId = GetRequiredGuid(requiredFields, "subtaskId");
        if (!subtaskId.HasValue)
        {
            return MissingApplyRequiredField("subtaskId");
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

        var targetSubtask = task.Subtasks
            .OrderBy(subtask => subtask.Order)
            .SingleOrDefault(subtask => subtask.Id == subtaskId.Value);
        if (targetSubtask is null)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read task detail summary again and pick an existing subtask.", "The requested subtask does not exist.");
        }

        var updatedInputs = task.Subtasks
            .OrderBy(subtask => subtask.Order)
            .Select(subtask => new TaskSubtaskInput(
                subtask.Id,
                subtask.Title,
                subtask.Id == subtaskId.Value ? isChecked : subtask.IsChecked,
                subtask.Order))
            .ToArray();

        var result = replaceTaskSubtasksCommandService.Replace(currentUserId, task.ProjectId, taskId.Value, updatedInputs);
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

        var changedTask = result.Task!;
        var changedFields = GetTaskSubtaskChangedFields(task, changedTask, subtaskId.Value);
        var actualDiff = BuildTaskSubtaskDiff(task, changedTask, subtaskId.Value);
        var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, changedTask.Id.ToString(), operation, "success", actualDiff);

        return PlainText(AiTextContractFormatter.ApplyResult(operation, targetType, changedTask.Id.ToString(), changedFields, auditEntryId));
    }

    private IResult ApplyReorderTask(
        ReorderTaskCommandService reorderTaskCommandService,
        AiAuditRegistry aiAuditRegistry,
        ITaskRepository taskRepository,
        string currentUserName,
        string sessionId,
        Guid currentUserId,
        string targetType,
        IReadOnlyDictionary<string, JsonElement> requiredFields,
        ProjectPresenceRegistry projectPresenceRegistry)
    {
        var taskId = GetRequiredGuid(requiredFields, "taskId");
        if (!taskId.HasValue)
        {
            return MissingApplyRequiredField("taskId");
        }

        var targetStateKey = GetRequiredString(requiredFields, "targetStateKey");
        if (targetStateKey is null)
        {
            return MissingApplyRequiredField("targetStateKey");
        }

        var targetIndex = GetRequiredInt32(requiredFields, "targetIndex");
        if (!targetIndex.HasValue)
        {
            return MissingApplyRequiredField("targetIndex");
        }

        if (targetIndex.Value < 0)
        {
            return ValidationFailed("targetIndex", "目標排序位置不可為負數");
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

        if (!string.Equals(task.CurrentState.Key, targetStateKey, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailed("targetStateKey", "任務目前不在指定的 workflow column，請先移動狀態再重新排序");
        }

        var orderedTasks = taskRepository.GetByProjectId(task.ProjectId)
            .Where(projectTask => string.Equals(projectTask.CurrentState.Key, targetStateKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(projectTask => projectTask.SortOrder)
            .ToList();

        var originalIndex = orderedTasks.FindIndex(projectTask => projectTask.Id == taskId.Value);
        if (originalIndex < 0)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        orderedTasks.RemoveAll(projectTask => projectTask.Id == taskId.Value);

        if (orderedTasks.Count == 0)
        {
            var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, task.Id.ToString(), "reorder_task", "success", [$"sort_order: {originalIndex} -> 0"]);
            return PlainText(AiTextContractFormatter.ApplyResult("reorder_task", targetType, task.Id.ToString(), ["sort_order"], auditEntryId));
        }

        if (targetIndex.Value >= orderedTasks.Count)
        {
            return ValidationFailed("targetIndex", "目標排序位置超出目前 workflow column 範圍");
        }

        var targetTaskId = orderedTasks[targetIndex.Value].Id;
        var result = reorderTaskCommandService.Reorder(currentUserId, task.ProjectId, taskId.Value, targetTaskId);

        if (result.ValidationError is not null)
        {
            return ValidationFailed(result.ValidationError.Field, result.ValidationError.Message);
        }

        if (result.TaskNotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick existing tasks before reordering.");
        }

        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        var changedTask = result.Task!;
    var auditEntryIdForReorder = aiAuditRegistry.Record(sessionId, currentUserName, targetType, changedTask.Id.ToString(), "reorder_task", "success", [$"sort_order: {originalIndex} -> {targetIndex.Value}"]);

        return PlainText(AiTextContractFormatter.ApplyResult("reorder_task", targetType, changedTask.Id.ToString(), ["sort_order"], auditEntryIdForReorder));
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
            return MissingApplyRequiredField("taskId");
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
    var auditEntryId = aiAuditRegistry.Record(sessionId, currentUserName, targetType, changedTask.Id.ToString(), operation, "success", actualDiff);

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

    private static IResult MissingApplyRequiredField(string fieldName)
    {
        return ErrorText(
            StatusCodes.Status400BadRequest,
            "ValidationFailed",
            $"Provide `requiredFields.{fieldName}` and submit the apply request again.",
            $"Required apply field `requiredFields.{fieldName}` is missing. POST /api/ai/apply reads `{fieldName}` from the `requiredFields` object, not from the top-level body.");
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

    private static int? GetRequiredInt32(IReadOnlyDictionary<string, JsonElement> fields, string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numericValue))
        {
            return numericValue;
        }

        return int.TryParse(GetOptionalString(fields, fieldName), out var parsedValue)
            ? parsedValue
            : null;
    }

    private static TaskCodeTraceability? GetOptionalCodeTraceability(
        IReadOnlyDictionary<string, JsonElement> fields,
        out ValidationError? validationError)
    {
        validationError = null;

        if (!fields.TryGetValue("codeTraceability", out var value))
        {
            return null;
        }

        return TaskCodeTraceabilityMapper.TryMap(value, out var codeTraceability, out validationError)
            ? codeTraceability
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

        if (optionalFields.ContainsKey("codeTraceability"))
        {
            changedFields.Add("codeTraceability");
        }

        return changedFields.Count == 0 ? ["title"] : changedFields;
    }

    private sealed record TaskDetailSnapshot(
        string Title,
        string Description,
        DateOnly? DueDate,
        string CodeTraceabilitySummary)
    {
        public static TaskDetailSnapshot FromTask(RonFlow.Domain.Task task)
        {
            return new(
                task.Title,
                task.Description,
                task.DueDate,
                DescribeTraceability(task.CodeTraceability.ToModel()));
        }
    }

    private static IReadOnlyList<string> BuildTaskDetailDiff(TaskDetailSnapshot task, CreateTaskOutput updatedTask, IReadOnlyList<string> changedFields)
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
                case "codeTraceability":
                    diff.Add($"codeTraceability: {task.CodeTraceabilitySummary} -> {DescribeTraceabilityOutput(updatedTask.CodeTraceability)}");
                    break;
            }
        }

        return diff;
    }

    private static string DescribeTraceability(TaskCodeTraceabilityModel codeTraceability)
    {
        return $"api:{codeTraceability.Api.Count}, frontendPages:{codeTraceability.FrontendPages.Count}, frontendComponents:{codeTraceability.FrontendComponents.Count}";
    }

    private static string DescribeTraceabilityOutput(TaskCodeTraceabilityOutput codeTraceability)
    {
        return $"api:{codeTraceability.Api.Count}, frontendPages:{codeTraceability.FrontendPages.Count}, frontendComponents:{codeTraceability.FrontendComponents.Count}";
    }

    private static IReadOnlyList<string> GetTaskSubtaskChangedFields(RonFlow.Domain.Task task, CreateTaskOutput updatedTask, Guid subtaskId)
    {
        var changedFields = new List<string> { "subtasks" };

        if (!string.Equals(task.CurrentState.Key, updatedTask.CurrentState.Key, StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("workflow_state_key");
        }

        return changedFields;
    }

    private static IReadOnlyList<string> BuildTaskSubtaskDiff(RonFlow.Domain.Task task, CreateTaskOutput updatedTask, Guid subtaskId)
    {
        var diff = new List<string>();
        var originalSubtask = task.Subtasks.Single(subtask => subtask.Id == subtaskId);
        var updatedSubtask = updatedTask.Subtasks.Single(subtask => subtask.Id == subtaskId);

        diff.Add($"subtask:{updatedSubtask.Title}: {(originalSubtask.IsChecked ? "checked" : "unchecked")} -> {(updatedSubtask.IsChecked ? "checked" : "unchecked")}");

        if (!string.Equals(task.CurrentState.Key, updatedTask.CurrentState.Key, StringComparison.OrdinalIgnoreCase))
        {
            diff.Add($"workflow_state_key: {NormalizeText(task.CurrentState.Key)} -> {NormalizeText(updatedTask.CurrentState.Key)}");
        }

        return diff;
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : value;
    }

    private static string NormalizeWorkflowStateKeyForCommand(string workflowStateKey)
    {
        return workflowStateKey.Trim().ToLowerInvariant() switch
        {
            "todo" => "todo",
            "active" => "active",
            "review" => "review",
            "done" => "done",
            _ => workflowStateKey,
        };
    }

    private static int CountOpenTasks(ProjectBoardView? board)
    {
        return board?.Columns
            .Where(column => !column.IsCompletedState)
            .Sum(column => column.Tasks.Count) ?? 0;
    }

    private static IResult PlainText(string content)
    {
        return Results.Text(content, "text/plain; charset=utf-8");
    }

    private IResult ContractResponse(string textContent, object jsonContent, string? format)
    {
        return WantsJson(format)
            ? Results.Json(jsonContent)
            : PlainText(textContent);
    }

    private bool WantsJson(string? format)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Request.Headers.Accept
            .ToString()
            .Contains("application/json", StringComparison.OrdinalIgnoreCase);
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
