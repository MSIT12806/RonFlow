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
            "你現在應先做以下事情：",
            "1. 讀取 capabilities manifest",
            "2. 讀取 glossary",
            "3. 查詢 session / scope summary",
            "4. 讀取 project list summary",
            string.Empty,
            "工作原則：",
            "- 先讀後寫",
            "- 先摘要後深入",
            "- 寫入前先確認 target object 與 required fields",
            string.Empty,
            "需要先問人的情況：",
            "- 無法判斷目標 Project 或 Task",
            "- 缺少必要輸入",
            "- 目前 scope 不正確",
            "- 權限不足",
        ]);
    }

    public static string CapabilitiesManifest()
    {
        return string.Join('\n',
        [
            "RonFlow Capabilities Manifest v1",
            string.Empty,
            "- capability: read_project_list_summary",
            "  category: read",
            "  active_scope_required: no",
            "  required_inputs: none",
            string.Empty,
            "- capability: read_project_board_summary",
            "  category: read",
            "  active_scope_required: yes",
            "  required_inputs: projectId",
            string.Empty,
            "- capability: create_project",
            "  category: write",
            "  active_scope_required: no",
            "  required_inputs: name",
            string.Empty,
            "- capability: create_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: projectId, title",
            string.Empty,
            "- capability: update_task_detail",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            "  optional_inputs: title, description, dueDate",
            string.Empty,
            "- capability: move_task_state",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId, targetStateKey",
            string.Empty,
            "- capability: reorder_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId, targetStateKey, targetIndex",
            string.Empty,
            "- capability: archive_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            string.Empty,
            "- capability: restore_archived_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            string.Empty,
            "- capability: trash_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
            string.Empty,
            "- capability: restore_trashed_task",
            "  category: write",
            "  active_scope_required: yes",
            "  required_inputs: taskId",
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
        builder.AppendLine("recent_activities:");

        foreach (var activity in task.ActivityTimeline.Take(2))
        {
            builder.AppendLine($"- {activity.Message}");
        }

        builder.AppendLine();
        builder.AppendLine("next_actions:");
        builder.AppendLine("- update_task_detail");
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