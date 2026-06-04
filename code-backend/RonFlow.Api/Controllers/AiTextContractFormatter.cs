using System.Text;
using RonFlow.Application;
using RonFlow.Domain;

namespace RonFlow.Api.Controllers;

internal static class AiTextContractFormatter
{
    public static string Bootstrap()
    {
        return string.Join('\n',
        [
            "RonFlow Bootstrap v1",
            string.Empty,
            "RonFlow 是一個專案管理工具。",
            "RonFlow 管理的主要工作物件有：Project、Board、Task、Session、Active Scope。",
            string.Empty,
            "canonical_base_paths:",
            "- ui_root_url: http://localhost/",
            "- ronflow_api_base_url: http://localhost/ronflow-api/api",
            "- ronauth_api_base_url: http://localhost/ronauth-api/api/auth",
            "- warning: http://localhost/ serves the UI shell; AI contracts live under /ronflow-api/api and auth lives under /ronauth-api/api/auth",
            string.Empty,
            "canonical_entrypoints:",
            "- GET /api/ai/bootstrap",
            "- GET /api/ai/glossary",
            "- GET /api/ai/capabilities",
            "- GET /api/ai/workflow-guidance",
            "- GET /api/ai/session-summary",
            "- GET /api/ai/projects/summary",
            "- GET /api/ai/invitations/summary",
            "- GET /api/ai/projects/{projectId}/board-summary",
            "- GET /api/ai/projects/{projectId}/current-work-summary",
            "- GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary",
            "- POST /api/ai/active-scope",
            "- POST /api/ai/apply",
            "- GET /api/ai/audit-entries/{auditEntryId}",
            string.Empty,
            "login_contract:",
            "- endpoint: POST http://localhost/ronauth-api/api/auth/login",
            "- request_body: {\"userName\":\"<user-name>\",\"password\":\"<password>\"}",
            "- note: RonAuth login reads `userName`, not `email`; email is identity metadata and may appear in summaries or responses",
            string.Empty,
            "你現在應先做以下事情：",
            "1. 讀取 capabilities manifest",
            "2. 讀取 glossary",
            "3. 讀取 workflow guidance",
            "4. 查詢 session / scope summary",
            "5. 讀取 project list summary",
            "6. 視需要讀取 invitation inbox summary",
            string.Empty,
            "工作原則：",
            "- 先讀後寫",
            "- 先摘要後深入",
            "- 寫入前先確認 target object 與 required fields",
            "- 後續細節以系統回傳的 contract 為準",
            string.Empty,
            "需要先問人的情況：",
            "- 無法判斷目標 Project 或 Task",
            "- 缺少必要輸入",
            "- 目前 scope 不正確",
            "- 權限不足",
        ]);
    }

    public static string Glossary()
    {
        return string.Join('\n',
        [
            "RonFlow AI Glossary v1",
            string.Empty,
            "- term: bootstrap",
            "  meaning: AI 進入 RonFlow 時的最小必要說明",
            "- term: capabilities manifest",
            "  meaning: 描述 AI 可用 read / write 能力與前置條件的契約",
            "- term: summary",
            "  meaning: 讓 AI 低成本理解上下文的查詢結果",
            "- term: active scope",
            "  meaning: AI 目前聚焦的 Project 或其他工作範圍",
            "- term: apply",
            "  meaning: 把已確認的變更正式寫入系統",
            "- term: audit entry",
            "  meaning: 記錄 AI 行為、理由與結果的紀錄單位",
            "- term: workflow guidance",
            "  meaning: 告訴 AI 在 RonFlow 中應如何遵循既有工作方式",
        ]);
    }

    public static string CapabilitiesManifest()
    {
        return string.Join('\n',
        [
            "RonFlow Capabilities Manifest v1",
            string.Empty,
            "scope_activation_contract:",
            "  endpoint: POST /api/ai/active-scope",
            "  body_shape: {\"projectId\":\"<project-id>\"}",
            "  success_status: 204 No Content",
            "  recovery_path: if the target project is not visible in project list summary, read invitation inbox summary before asking the human for access",
            string.Empty,
            "write_request_contract:",
            "  apply_endpoint: POST /api/ai/apply",
            "  body_shape: operation, targetType, targetId, requiredFields, optionalFields, note",
            "  required_input_location: requiredFields.<inputName>",
            string.Empty,
            "- capability: read_session_summary",
            "  category: read",
            "  active_scope_required: no",
            "  required_inputs: none",
            "  read_endpoint: GET /api/ai/session-summary",
            "  route_params: none",
            "  yields: active_scope, available_scopes",
            string.Empty,
            "- capability: read_task_detail_summary",
            "  category: read",
            "  active_scope_required: yes",
            "  required_inputs: projectId, taskId",
            "  read_endpoint: GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary",
            "  route_params: projectId, taskId",
            "  route_param_sources:",
            "  - projectId <- read_project_list_summary.project_id or read_session_summary.active_scope",
            "  - taskId <- read_project_board_summary.visible_tasks.task_id or read_current_work_summary.open_tasks.task_id",
            string.Empty,
            "- capability: read_current_work_summary",
            "  category: read",
            "  active_scope_required: yes",
            "  required_inputs: projectId",
            "  read_endpoint: GET /api/ai/projects/{projectId}/current-work-summary",
            "  route_params: projectId",
            "  route_param_sources:",
            "  - projectId <- read_project_list_summary.project_id or read_session_summary.active_scope",
            string.Empty,
            "- capability: read_project_list_summary",
            "  category: read",
            "  active_scope_required: no",
            "  required_inputs: none",
            "  read_endpoint: GET /api/ai/projects/summary",
            "  route_params: none",
            "  yields: project_id",
            string.Empty,
            "- capability: read_project_board_summary",
            "  category: read",
            "  active_scope_required: yes",
            "  required_inputs: projectId",
            "  read_endpoint: GET /api/ai/projects/{projectId}/board-summary",
            "  route_params: projectId",
            "  route_param_sources:",
            "  - projectId <- read_project_list_summary.project_id or read_session_summary.active_scope",
            string.Empty,
            "- capability: read_audit_entry",
            "  category: read",
            "  active_scope_required: yes",
            "  required_inputs: auditEntryId",
            "  read_endpoint: GET /api/ai/audit-entries/{auditEntryId}",
            "  route_params: auditEntryId",
            "  route_param_sources:",
            "  - auditEntryId <- apply_result.audit_entry_id",
            string.Empty,
            "- capability: read_invitation_inbox_summary",
            "  category: read",
            "  active_scope_required: no",
            "  required_inputs: none",
            "  read_endpoint: GET /api/ai/invitations/summary",
            "  route_params: none",
            "  yields: invitation_id, project_id",
            string.Empty,
            "- capability: create_project",
            "  category: write",
            "  active_scope_required: no",
            "  required_inputs: name",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.name",
            "  apply_request_example: {\"operation\":\"create_project\",\"targetType\":\"project\",\"targetId\":\"new\",\"requiredFields\":{\"name\":\"<project-name>\"},\"optionalFields\":{},\"note\":\"create project\"}",
            string.Empty,
            "- capability: create_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: projectId, title",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.projectId, requiredFields.title",
            "  apply_request_example: {\"operation\":\"create_task\",\"targetType\":\"task\",\"targetId\":\"new\",\"requiredFields\":{\"projectId\":\"<project-id>\",\"title\":\"<task-title>\"},\"optionalFields\":{},\"note\":\"create task\"}",
            string.Empty,
            "- capability: invite_project_member",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: projectId, invitee",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.projectId, requiredFields.invitee",
            string.Empty,
            "- capability: accept_project_invitation",
            "  category: write",
            "  active_scope_required: no",
            "  required_inputs: invitationId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.invitationId",
            "  apply_request_example: {\"operation\":\"accept_project_invitation\",\"targetType\":\"invitation\",\"targetId\":\"<invitation-id>\",\"requiredFields\":{\"invitationId\":\"<invitation-id>\"},\"optionalFields\":{},\"note\":\"accept invitation\"}",
            string.Empty,
            "- capability: reject_project_invitation",
            "  category: write",
            "  active_scope_required: no",
            "  required_inputs: invitationId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.invitationId",
            "  apply_request_example: {\"operation\":\"reject_project_invitation\",\"targetType\":\"invitation\",\"targetId\":\"<invitation-id>\",\"requiredFields\":{\"invitationId\":\"<invitation-id>\"},\"optionalFields\":{},\"note\":\"reject invitation\"}",
            string.Empty,
            "- capability: update_task_detail",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            "  optional_inputs: title, description, dueDate",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId",
            "  optional_fields_path: optionalFields.title, optionalFields.description, optionalFields.dueDate",
            "  apply_request_example: {\"operation\":\"update_task_detail\",\"targetType\":\"task\",\"targetId\":\"<task-id>\",\"requiredFields\":{\"taskId\":\"<task-id>\"},\"optionalFields\":{\"title\":\"<new-title>\"},\"note\":\"update task detail\"}",
            string.Empty,
            "- capability: check_task_subtask",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId, subtaskId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId, requiredFields.subtaskId",
            string.Empty,
            "- capability: uncheck_task_subtask",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId, subtaskId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId, requiredFields.subtaskId",
            string.Empty,
            "- capability: move_task_state",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId, targetStateKey",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId, requiredFields.targetStateKey",
            "  apply_request_example: {\"operation\":\"move_task_state\",\"targetType\":\"task\",\"targetId\":\"<task-id>\",\"requiredFields\":{\"taskId\":\"<task-id>\",\"targetStateKey\":\"Done\"},\"optionalFields\":{},\"note\":\"move task state\"}",
            string.Empty,
            "- capability: reorder_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId, targetStateKey, targetIndex",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId, requiredFields.targetStateKey, requiredFields.targetIndex",
            string.Empty,
            "- capability: archive_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId",
            string.Empty,
            "- capability: restore_archived_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId",
            string.Empty,
            "- capability: trash_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId",
            string.Empty,
            "- capability: restore_trashed_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            "  apply_endpoint: POST /api/ai/apply",
            "  required_fields_path: requiredFields.taskId",
        ]);
    }

    public static string WorkflowGuidance()
    {
        return string.Join('\n',
        [
            "RonFlow Workflow Guidance v1",
            string.Empty,
            "recommended_order:",
            "1. read summary",
            "2. confirm target object",
            "3. confirm required fields",
            "4. prepare write request",
            "5. apply",
            "6. inspect result",
            string.Empty,
            "canonical_discovery_path:",
            "- login -> POST http://localhost/ronauth-api/api/auth/login",
            "- activate session -> POST /api/session/activate with X-RonFlow-Session-Id",
            "- bootstrap -> GET /api/ai/bootstrap",
            "- discover contracts -> GET /api/ai/capabilities, GET /api/ai/glossary, GET /api/ai/workflow-guidance",
            "- inspect session -> GET /api/ai/session-summary",
            "- discover projects -> GET /api/ai/projects/summary -> yields projectId",
            "- activate target project scope -> POST /api/ai/active-scope with projectId",
            "- inspect scoped work -> GET /api/ai/projects/{projectId}/board-summary or GET /api/ai/projects/{projectId}/current-work-summary -> yields taskId",
            "- inspect task detail -> GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary",
            string.Empty,
            "checklist_rules:",
            "- if task detail summary contains subtasks, treat them as the execution plan before declaring the task complete",
            "- use check_task_subtask when one checklist item is finished",
            "- use uncheck_task_subtask when a previously checked checklist item is no longer satisfied",
            "- do not report the task as fully complete while required subtasks remain unchecked",
            string.Empty,
            "invitation_rules:",
            "- if project list summary does not contain the expected project, read_invitation_inbox_summary before asking the human for access",
            "- use accept_project_invitation only when the invitation inbox summary contains the target invitation",
            "- use reject_project_invitation only when the human intent is to decline that invitation",
            string.Empty,
            "ask_human_when:",
            "- target object is ambiguous",
            "- required input is missing",
            "- current scope is not correct",
            "- returned error is not understood",
        ]);
    }

    public static string SessionSummary(string actorIdentity, Guid? activeScope, IReadOnlyList<Guid> availableScopes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RonFlow Session Summary v1");
        builder.AppendLine();
        builder.AppendLine("session_status: active");
        builder.AppendLine("actor_type: ai");
        builder.AppendLine($"actor_identity: {actorIdentity}");
        builder.AppendLine($"active_scope: {(activeScope.HasValue ? activeScope.Value.ToString() : "none")}");
        builder.AppendLine("available_scopes:");

        foreach (var scope in availableScopes)
        {
            builder.AppendLine($"- {scope}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string ProjectListSummary(ProjectListView projects, Func<Guid, int> openTaskCountProvider)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RonFlow Project List Summary v1");
        builder.AppendLine();
        builder.AppendLine($"projects_count: {projects.Items.Count}");
        builder.AppendLine();

        foreach (var project in projects.Items)
        {
            builder.AppendLine($"- project_id: {project.Id}");
            builder.AppendLine($"  project_name: {project.Name}");
            builder.AppendLine($"  role: {NormalizeRole(project.Role)}");
            builder.AppendLine($"  open_task_count: {openTaskCountProvider(project.Id)}");
            builder.AppendLine();
        }

        builder.AppendLine("next_actions:");
        builder.AppendLine("- read_project_board_summary");
        builder.AppendLine("- create_project");
        builder.AppendLine("- activate_scope");

        return builder.ToString().TrimEnd();
    }

    public static string ProjectBoardSummary(ProjectBoardView board)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RonFlow Project Board Summary v1");
        builder.AppendLine();
        builder.AppendLine($"project_id: {board.ProjectId}");
        builder.AppendLine($"project_name: {board.ProjectName}");
        builder.AppendLine("workflow_columns:");

        foreach (var column in board.Columns)
        {
            builder.AppendLine($"- key: {NormalizeWorkflowKey(column.StateKey)}");
            builder.AppendLine($"  name: {column.Label}");
            builder.AppendLine($"  task_count: {column.Tasks.Count}");
        }

        builder.AppendLine();
        builder.AppendLine("visible_tasks:");

        foreach (var task in board.Columns
                     .SelectMany(column => column.Tasks.Select(task => new
                     {
                         Task = task,
                         WorkflowStateKey = NormalizeWorkflowKey(column.StateKey),
                     })))
        {
            builder.AppendLine($"- task_id: {task.Task.Id}");
            builder.AppendLine($"  title: {task.Task.Title}");
            builder.AppendLine($"  workflow_state_key: {task.WorkflowStateKey}");
        }

        builder.AppendLine();
        builder.AppendLine("recent_activities:");

        foreach (var activity in board.Columns
                     .SelectMany(column => column.Tasks)
                     .Take(2)
                     .Select(task => $"Task visible on board: {task.Title}"))
        {
            builder.AppendLine($"- {activity}");
        }

        builder.AppendLine();
        builder.AppendLine("next_actions:");
        builder.AppendLine("- read_task_detail_summary");
        builder.AppendLine("- create_task");
        builder.AppendLine("- move_task_state");

        return builder.ToString().TrimEnd();
    }

    public static string InvitationInboxSummary(InvitationInboxView inbox)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RonFlow Invitation Inbox Summary v1");
        builder.AppendLine();
        builder.AppendLine($"pending_invitation_count: {inbox.Items.Count}");
        builder.AppendLine("invitations:");

        foreach (var invitation in inbox.Items)
        {
            builder.AppendLine($"- invitation_id: {invitation.Id}");
            builder.AppendLine($"  project_id: {invitation.ProjectId}");
            builder.AppendLine($"  project_name: {invitation.ProjectName}");
            builder.AppendLine($"  inviter_name: {invitation.InviterName}");
        }

        builder.AppendLine("next_actions:");
        builder.AppendLine("- accept_project_invitation");
        builder.AppendLine("- reject_project_invitation");
        builder.AppendLine("- read_project_list_summary");

        return builder.ToString().TrimEnd();
    }

    public static string CurrentWorkSummary(ProjectBoardView board)
    {
        var builder = new StringBuilder();
        var openTasks = board.Columns
            .Where(column => !column.IsCompletedState)
            .SelectMany(column => column.Tasks.Select(task => new
            {
                Task = task,
                WorkflowStateKey = NormalizeWorkflowKey(column.StateKey),
            }))
            .ToArray();

        builder.AppendLine("RonFlow Current Work Summary v1");
        builder.AppendLine();
        builder.AppendLine($"project_id: {board.ProjectId}");
        builder.AppendLine($"project_name: {board.ProjectName}");
        builder.AppendLine($"open_task_count: {openTasks.Length}");
        builder.AppendLine();
        builder.AppendLine("open_tasks:");

        foreach (var task in openTasks)
        {
            builder.AppendLine($"- task_id: {task.Task.Id}");
            builder.AppendLine($"  title: {task.Task.Title}");
            builder.AppendLine($"  workflow_state_key: {task.WorkflowStateKey}");
        }

        builder.AppendLine();
        builder.AppendLine("next_actions:");
        builder.AppendLine("- read_task_detail_summary");
        builder.AppendLine("- update_task_detail");
        builder.AppendLine("- move_task_state");

        return builder.ToString().TrimEnd();
    }

    public static string TaskDetailSummary(TaskDetailView task)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RonFlow Task Detail Summary v1");
        builder.AppendLine();
        builder.AppendLine($"task_id: {task.Id}");
        builder.AppendLine($"title: {task.Title}");
        builder.AppendLine($"description: {task.Description}");
        builder.AppendLine($"due_date: {(task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy-MM-dd") : "none")}");
        builder.AppendLine($"workflow_state_key: {NormalizeWorkflowKey(task.CurrentState.Key)}");
        builder.AppendLine($"workflow_state_name: {task.CurrentState.Label}");
        builder.AppendLine($"lifecycle_state: {NormalizeLifecycleState(task.LifecycleState)}");
        builder.AppendLine("subtasks:");

        foreach (var subtask in task.Subtasks.OrderBy(subtask => subtask.Order))
        {
            builder.AppendLine($"- subtask_id: {subtask.Id}");
            builder.AppendLine($"  title: {subtask.Title}");
            builder.AppendLine($"  is_checked: {(subtask.IsChecked ? "yes" : "no")}");
            builder.AppendLine($"  order: {subtask.Order}");
        }

        builder.AppendLine("recent_activities:");

        foreach (var activity in task.ActivityTimeline.Take(2))
        {
            builder.AppendLine($"- {activity.Message}");
        }

        builder.AppendLine();
        builder.AppendLine("next_actions:");
        builder.AppendLine("- update_task_detail");
        if (task.Subtasks.Count > 0)
        {
            builder.AppendLine("- check_task_subtask");
            builder.AppendLine("- uncheck_task_subtask");
        }
        builder.AppendLine("- move_task_state");
        builder.AppendLine("- reorder_task");
        builder.AppendLine("- archive_task");
        builder.AppendLine("- trash_task");

        return builder.ToString().TrimEnd();
    }

    public static string Error(string errorCode, string message, string recoveryHint)
    {
        return string.Join('\n',
        [
            "RonFlow Error v1",
            string.Empty,
            $"error_code: {errorCode}",
            $"message: {message}",
            $"recovery_hint: {recoveryHint}",
        ]);
    }

    public static string ApplyResult(string operation, string targetType, string targetId, IReadOnlyList<string> changedFields, Guid auditEntryId)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RonFlow Apply Result v1");
        builder.AppendLine();
        builder.AppendLine("status: success");
        builder.AppendLine($"operation: {operation}");
        builder.AppendLine($"target_type: {targetType}");
        builder.AppendLine($"target_id: {targetId}");
        builder.AppendLine("changed_fields:");

        foreach (var changedField in changedFields)
        {
            builder.AppendLine($"- {changedField}");
        }

        builder.AppendLine($"audit_entry_id: {auditEntryId}");

        return builder.ToString().TrimEnd();
    }

    public static string AuditEntry(AiAuditEntry auditEntry)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RonFlow Audit Entry v1");
        builder.AppendLine();
        builder.AppendLine($"audit_entry_id: {auditEntry.Id}");
        builder.AppendLine($"actor_type: {auditEntry.ActorType}");
        builder.AppendLine($"actor_identity: {auditEntry.ActorIdentity}");
        builder.AppendLine($"target_type: {auditEntry.TargetType}");
        builder.AppendLine($"target_id: {auditEntry.TargetId}");
        builder.AppendLine($"requested_change: {auditEntry.RequestedChange}");
        builder.AppendLine($"result_status: {auditEntry.ResultStatus}");
        builder.AppendLine("actual_diff:");

        foreach (var diff in auditEntry.ActualDiff)
        {
            builder.AppendLine($"- {diff}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string NormalizeRole(string role)
    {
        return role == "專案擁有者" ? "owner" : "member";
    }

    private static string NormalizeWorkflowKey(string workflowKey)
    {
        return workflowKey.ToLowerInvariant() switch
        {
            "todo" => "Todo",
            "active" => "Active",
            "review" => "Review",
            "done" => "Done",
            _ => workflowKey,
        };
    }

    private static string NormalizeLifecycleState(TaskLifecycleState lifecycleState)
    {
        return lifecycleState switch
        {
            TaskLifecycleState.ActiveRecord => "active",
            TaskLifecycleState.Archived => "archived",
            TaskLifecycleState.Trashed => "trashed",
            _ => lifecycleState.ToString(),
        };
    }
}
