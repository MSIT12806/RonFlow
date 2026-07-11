using RonFlow.Application;

namespace RonFlow.Api.Controllers;

internal static class AiJsonContractFormatter
{
    public static object Bootstrap()
    {
        return new
        {
            version = "RonFlow Bootstrap v1",
            domain = "RonFlow 是一個專案管理工具。",
            managedObjects = new[] { "Project", "Board", "Task", "Session", "Active Scope" },
            canonicalBasePaths = new
            {
                uiRootUrl = "http://localhost/",
                ronflowApiBaseUrl = "http://localhost/ronflow-api/api",
                ronauthApiBaseUrl = "http://localhost/ronauth-api/api/auth",
                warning = "http://localhost/ serves the UI shell; AI contracts live under /ronflow-api/api and auth lives under /ronauth-api/api/auth",
            },
            canonicalEntrypoints = new[]
            {
                "GET /api/ai/bootstrap",
                "GET /api/ai/glossary",
                "GET /api/ai/capabilities",
                "GET /api/ai/workflow-guidance",
                "GET /api/ai/session-summary",
                "GET /api/ai/projects/summary",
                "GET /api/ai/invitations/summary",
                "GET /api/ai/projects/{projectId}/board-summary",
                "GET /api/ai/projects/{projectId}/current-work-summary",
                "GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary",
                "POST /api/ai/active-scope",
                "POST /api/ai/apply",
                "GET /api/ai/audit-entries/{auditEntryId}",
            },
            loginContract = new
            {
                endpoint = "POST http://localhost/ronauth-api/api/auth/login",
                requestBody = new { userName = "<user-name>", password = "<password>" },
                note = "RonAuth login reads userName, not email; email is identity metadata and may appear in summaries or responses",
            },
            nextSteps = new[]
            {
                "read_capabilities_manifest",
                "read_glossary",
                "read_workflow_guidance",
                "read_session_summary",
                "read_project_list_summary",
                "read_invitation_inbox_summary_when_needed",
            },
            principles = new[] { "先讀後寫", "先摘要後深入", "寫入前先確認 target object 與 required fields", "後續細節以系統回傳的 contract 為準" },
            askHumanWhen = new[] { "無法判斷目標 Project 或 Task", "缺少必要輸入", "目前 scope 不正確", "權限不足" },
        };
    }

    public static object Glossary()
    {
        return new
        {
            version = "RonFlow AI Glossary v1",
            terms = new[]
            {
                new { term = "bootstrap", meaning = "AI 進入 RonFlow 時的最小必要說明" },
                new { term = "capabilities manifest", meaning = "描述 AI 可用 read / write 能力與前置條件的契約" },
                new { term = "summary", meaning = "讓 AI 低成本理解上下文的查詢結果" },
                new { term = "active scope", meaning = "AI 目前聚焦的 Project 或其他工作範圍" },
                new { term = "apply", meaning = "把已確認的變更正式寫入系統" },
                new { term = "audit entry", meaning = "記錄 AI 行為、理由與結果的紀錄單位" },
                new { term = "workflow guidance", meaning = "告訴 AI 在 RonFlow 中應如何遵循既有工作方式" },
            },
        };
    }

    public static object CapabilitiesManifest()
    {
        return new
        {
            version = "RonFlow Capabilities Manifest v1",
            scopeActivationContract = new
            {
                endpoint = "POST /api/ai/active-scope",
                bodyShape = new { projectId = "<project-id>" },
                successStatus = "204 No Content",
                recoveryPath = "if the target project is not visible in project list summary, read invitation inbox summary before asking the human for access",
            },
            writeRequestContract = new
            {
                applyEndpoint = "POST /api/ai/apply",
                bodyShape = new[] { "operation", "targetType", "targetId", "requiredFields", "optionalFields", "note" },
                requiredInputLocation = "requiredFields.<inputName>",
            },
            capabilities = new object[]
            {
                ReadCapability("read_session_summary", "GET /api/ai/session-summary", false, [], [], ["active_scope", "available_scopes"]),
                ReadCapability("read_task_detail_summary", "GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary", true, ["projectId", "taskId"], ["projectId", "taskId"], []),
                ReadCapability("read_current_work_summary", "GET /api/ai/projects/{projectId}/current-work-summary", true, ["projectId"], ["projectId"], []),
                ReadCapability("read_project_list_summary", "GET /api/ai/projects/summary", false, [], [], ["project_id"]),
                ReadCapability("read_project_board_summary", "GET /api/ai/projects/{projectId}/board-summary", true, ["projectId"], ["projectId"], []),
                ReadCapability("read_audit_entry", "GET /api/ai/audit-entries/{auditEntryId}", true, ["auditEntryId"], ["auditEntryId"], []),
                ReadCapability("read_invitation_inbox_summary", "GET /api/ai/invitations/summary", false, [], [], ["invitation_id", "project_id"]),
                WriteCapability("create_project", "project", false, ["name"], [], new { operation = "create_project", targetType = "project", targetId = "new", requiredFields = new { name = "<project-name>" }, optionalFields = new { }, note = "create project" }),
                WriteCapability("create_task", "task", true, ["projectId", "title"], [], new { operation = "create_task", targetType = "task", targetId = "new", requiredFields = new { projectId = "<project-id>", title = "<task-title>" }, optionalFields = new { }, note = "create task" }),
                WriteCapability("invite_project_member", "project", true, ["projectId", "invitee"], [], new { operation = "invite_project_member", targetType = "project", targetId = "<project-id>", requiredFields = new { projectId = "<project-id>", invitee = "<email-or-user-name>" }, optionalFields = new { }, note = "invite member" }),
                WriteCapability("accept_project_invitation", "invitation", false, ["invitationId"], [], new { operation = "accept_project_invitation", targetType = "invitation", targetId = "<invitation-id>", requiredFields = new { invitationId = "<invitation-id>" }, optionalFields = new { }, note = "accept invitation" }),
                WriteCapability("reject_project_invitation", "invitation", false, ["invitationId"], [], new { operation = "reject_project_invitation", targetType = "invitation", targetId = "<invitation-id>", requiredFields = new { invitationId = "<invitation-id>" }, optionalFields = new { }, note = "reject invitation" }),
                WriteCapability("update_task_detail", "task", true, ["taskId"], ["title", "description", "dueDate", "estimatedEffort", "codeTraceability"], new { operation = "update_task_detail", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>" }, optionalFields = new { title = "<new-title>", estimatedEffort = new { value = 5, unit = "hours" }, codeTraceability = CodeTraceabilityExample() }, note = "update task detail" }, [EstimatedEffortFieldSchema()]),
                WriteCapability("check_task_subtask", "task", true, ["taskId", "subtaskId"], [], new { operation = "check_task_subtask", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>", subtaskId = "<subtask-id>" }, optionalFields = new { }, note = "check subtask" }),
                WriteCapability("uncheck_task_subtask", "task", true, ["taskId", "subtaskId"], [], new { operation = "uncheck_task_subtask", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>", subtaskId = "<subtask-id>" }, optionalFields = new { }, note = "uncheck subtask" }),
                WriteCapability("move_task_state", "task", true, ["taskId", "targetStateKey"], [], new { operation = "move_task_state", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>", targetStateKey = "Done" }, optionalFields = new { }, note = "move task state" }),
                WriteCapability("reorder_task", "task", true, ["taskId", "targetStateKey", "targetIndex"], [], new { operation = "reorder_task", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>", targetStateKey = "Todo", targetIndex = 0 }, optionalFields = new { }, note = "reorder task" }),
                WriteCapability("archive_task", "task", true, ["taskId"], [], new { operation = "archive_task", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>" }, optionalFields = new { }, note = "archive task" }),
                WriteCapability("restore_archived_task", "task", true, ["taskId"], [], new { operation = "restore_archived_task", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>" }, optionalFields = new { }, note = "restore archived task" }),
                WriteCapability("trash_task", "task", true, ["taskId"], [], new { operation = "trash_task", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>" }, optionalFields = new { }, note = "trash task" }),
                WriteCapability("restore_trashed_task", "task", true, ["taskId"], [], new { operation = "restore_trashed_task", targetType = "task", targetId = "<task-id>", requiredFields = new { taskId = "<task-id>" }, optionalFields = new { }, note = "restore trashed task" }),
            },
        };
    }

    public static object WorkflowGuidance()
    {
        return new
        {
            version = "RonFlow Workflow Guidance v1",
            recommendedOrder = new[] { "read summary", "confirm target object", "confirm required fields", "prepare write request", "apply", "inspect result" },
            canonicalDiscoveryPath = new[]
            {
                "login -> POST http://localhost/ronauth-api/api/auth/login",
                "activate session -> POST /api/session/activate with X-RonFlow-Session-Id",
                "bootstrap -> GET /api/ai/bootstrap",
                "discover contracts -> GET /api/ai/capabilities, GET /api/ai/glossary, GET /api/ai/workflow-guidance",
                "inspect session -> GET /api/ai/session-summary",
                "discover projects -> GET /api/ai/projects/summary -> yields projectId",
                "activate target project scope -> POST /api/ai/active-scope with projectId",
                "inspect scoped work -> GET /api/ai/projects/{projectId}/board-summary or GET /api/ai/projects/{projectId}/current-work-summary -> yields taskId",
                "inspect task detail -> GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary",
            },
            taskStartRules = new[]
            {
                "when the human asks the AI to complete a RonFlow task, ensure the confirmed task is already in Flow Active before implementation work begins",
                "if the task is still in Hatchery, fill the missing ready fields first, enter Flow, and then move it to Active before implementation work begins",
                "if the task is already in Flow Todo, move it to Active before implementation work begins",
                "use move_task_state with targetStateKey: Active",
                "inspect the apply result before continuing implementation work",
                "skip this only when the human is decomposing, planning, clarifying, or estimating the task instead of completing it; the target task is ambiguous; the project has no Active state; or the human explicitly asks not to change state",
            },
            checklistRules = new[]
            {
                "if task detail summary contains subtasks, treat them as the execution plan before declaring the task complete",
                "use check_task_subtask when one checklist item is finished",
                "do not report the task as fully complete while required subtasks remain unchecked",
            },
        };
    }

    public static object SessionSummary(string actorIdentity, Guid? activeScope, IReadOnlyList<Guid> availableScopes)
    {
        return new
        {
            version = "RonFlow Session Summary v1",
            canonicalEndpoint = "GET /api/ai/session-summary",
            sessionStatus = "active",
            actorType = "ai",
            actorIdentity,
            activeScope,
            availableScopes,
        };
    }

    public static object ProjectListSummary(
        ProjectListView projects,
        Func<Guid, int> openTaskCountProvider,
        Func<Guid, int> hatcheryTaskCountProvider)
    {
        return new
        {
            version = "RonFlow Project List Summary v1",
            canonicalEndpoint = "GET /api/ai/projects/summary",
            projectsCount = projects.Items.Count,
            projects = projects.Items.Select(project => new
            {
                projectId = project.Id,
                projectName = project.Name,
                role = NormalizeRole(project.Role),
                openTaskCount = openTaskCountProvider(project.Id),
                hatcheryTaskCount = hatcheryTaskCountProvider(project.Id),
            }).ToArray(),
            nextActions = new[] { "read_project_board_summary", "create_project", "activate_scope" },
        };
    }

    public static object InvitationInboxSummary(InvitationInboxView inbox)
    {
        return new
        {
            version = "RonFlow Invitation Inbox Summary v1",
            canonicalEndpoint = "GET /api/ai/invitations/summary",
            pendingInvitationCount = inbox.Items.Count,
            invitations = inbox.Items.Select(invitation => new
            {
                invitationId = invitation.Id,
                projectId = invitation.ProjectId,
                projectName = invitation.ProjectName,
                inviterName = invitation.InviterName,
            }).ToArray(),
            nextActions = new[] { "accept_project_invitation", "reject_project_invitation", "read_project_list_summary" },
        };
    }

    public static object ProjectBoardSummary(ProjectBoardView board)
    {
        return new
        {
            version = "RonFlow Project Board Summary v1",
            canonicalEndpoint = $"GET /api/ai/projects/{board.ProjectId}/board-summary",
            projectId = board.ProjectId,
            projectName = board.ProjectName,
            workflowColumns = board.Columns.Select(column => new
            {
                key = NormalizeWorkflowKey(column.StateKey),
                name = column.Label,
                taskCount = column.Tasks.Count,
                isCompletedState = column.IsCompletedState,
            }).ToArray(),
            taskTreeTasks = FlattenTaskTree(board.TaskTree).Select(task => new
            {
                taskId = task.Id,
                title = task.Title,
                isInFlow = false,
                hatcheryStatus = DescribeHatcheryStatus(task),
                readyToEnterFlow = IsReadyLeafTask(task),
                completionConditionCount = task.CompletionConditionCount,
                hasEstimatedEffort = task.HasEstimatedEffort,
                missingReadyFields = GetMissingReadyFields(task),
                readyFieldFormats = GetReadyFieldFormats(task),
                childCount = task.Children.Count,
            }).ToArray(),
            visibleTasks = board.Columns
                .SelectMany(column => column.Tasks.Select(task => new
                {
                    taskId = task.Id,
                    title = task.Title,
                    workflowStateKey = NormalizeWorkflowKey(column.StateKey),
                    isInFlow = true,
                }))
                .ToArray(),
            nextActions = new[] { "read_task_detail_summary", "create_task", "move_task_state" },
        };
    }

    public static object CurrentWorkSummary(ProjectBoardView board)
    {
        var hatcheryTasks = FlattenTaskTree(board.TaskTree)
            .Select(task => new
            {
                taskId = task.Id,
                title = task.Title,
                isInFlow = false,
                hatcheryStatus = DescribeHatcheryStatus(task),
                readyToEnterFlow = IsReadyLeafTask(task),
                completionConditionCount = task.CompletionConditionCount,
                hasEstimatedEffort = task.HasEstimatedEffort,
                missingReadyFields = GetMissingReadyFields(task),
                readyFieldFormats = GetReadyFieldFormats(task),
            })
            .ToArray();
        var openTasks = board.Columns
            .Where(column => !column.IsCompletedState)
            .SelectMany(column => column.Tasks.Select(task => new
            {
                taskId = task.Id,
                title = task.Title,
                workflowStateKey = NormalizeWorkflowKey(column.StateKey),
                isInFlow = true,
                executionStatus = "executable_in_flow",
            }))
            .ToArray();

        return new
        {
            version = "RonFlow Current Work Summary v1",
            canonicalEndpoint = $"GET /api/ai/projects/{board.ProjectId}/current-work-summary",
            projectId = board.ProjectId,
            projectName = board.ProjectName,
            openTaskCount = openTasks.Length,
            hatcheryTaskCount = hatcheryTasks.Length,
            openTasks,
            hatcheryTasks,
            nextActions = new[] { "read_task_detail_summary", "update_task_detail", "move_task_state" },
        };
    }

    public static object TaskDetailSummary(TaskDetailView task)
    {
        var isInFlow = task.IsInFlow;
        var workflowStateKey = NormalizeWorkflowKey(task.CurrentState.Key);
        var nextActions = BuildTaskDetailNextActions(task).ToArray();

        return new
        {
            version = "RonFlow Task Detail Summary v1",
            canonicalEndpoint = $"GET /api/ai/projects/{task.ProjectId}/tasks/{task.Id}/detail-summary",
            taskId = task.Id,
            projectId = task.ProjectId,
            title = task.Title,
            description = task.Description,
            dueDate = task.DueDate?.ToString("yyyy-MM-dd"),
            estimatedEffort = task.EstimatedEffort is null ? null : new { task.EstimatedEffort.Value, task.EstimatedEffort.Unit },
            isInFlow,
            flowMembershipStatus = isInFlow ? "in_flow" : "hatchery",
            workflowStateKey = isInFlow ? workflowStateKey : null,
            workflowStateName = isInFlow ? task.CurrentState.Label : null,
            hatcheryStatus = DescribeHatcheryStatus(task),
            readyToEnterFlow = IsReadyLeafTask(task),
            completionConditionCount = task.Subtasks.Count,
            hasEstimatedEffort = task.EstimatedEffort is not null,
            missingReadyFields = GetMissingReadyFields(task),
            readyFieldFormats = GetReadyFieldFormats(task),
            lifecycleState = NormalizeLifecycleState(task.LifecycleState),
            subtasks = task.Subtasks.OrderBy(subtask => subtask.Order).Select(subtask => new
            {
                subtaskId = subtask.Id,
                title = subtask.Title,
                isChecked = subtask.IsChecked,
                order = subtask.Order,
            }).ToArray(),
            codeTraceabilitySummary = CodeTraceabilitySummary(task.CodeTraceability),
            recentActivities = task.ActivityTimeline.Take(2).Select(activity => activity.Message).ToArray(),
            nextActions,
        };
    }

    private static object ReadCapability(
        string name,
        string readEndpoint,
        bool activeScopeRequired,
        string[] requiredInputs,
        string[] routeParams,
        string[] yields)
    {
        return new
        {
            name,
            category = "read",
            activeScopeRequired,
            requiredInputs,
            routeParams,
            yields,
            readEndpoint,
        };
    }

    private static object WriteCapability(
        string operation,
        string targetType,
        bool activeScopeRequired,
        string[] requiredFields,
        string[] optionalFields,
        object exampleRequest,
        object[]? fieldSchemas = null)
    {
        return new
        {
            name = operation,
            category = "write",
            operation,
            targetType,
            activeScopeRequired,
            requiredFields,
            optionalFields,
            requiredFieldsPath = requiredFields.Select(field => $"requiredFields.{field}").ToArray(),
            optionalFieldsPath = optionalFields.Select(field => $"optionalFields.{field}").ToArray(),
            fieldSchemas = fieldSchemas ?? Array.Empty<object>(),
            applyEndpoint = "POST /api/ai/apply",
            exampleRequest,
        };
    }

    private static object EstimatedEffortFieldSchema()
    {
        return new
        {
            name = "estimatedEffort",
            path = "optionalFields.estimatedEffort",
            type = "object",
            requiredProperties = new
            {
                value = "positive integer",
                unit = "minutes | hours | days",
            },
            example = new { value = 5, unit = "hours" },
            invalidExamples = new object[] { "5h", "5 hours" },
        };
    }

    private static object CodeTraceabilityExample()
    {
        return new
        {
            api = new[] { new { changeType = "modified", target = "GET /api/ai/capabilities" } },
            frontendPages = Array.Empty<object>(),
            frontendComponents = Array.Empty<object>(),
        };
    }

    private static object CodeTraceabilitySummary(TaskCodeTraceabilityView codeTraceability)
    {
        return new
        {
            api = TraceabilityCategorySummary(codeTraceability.Api),
            frontendPages = TraceabilityCategorySummary(codeTraceability.FrontendPages),
            frontendComponents = TraceabilityCategorySummary(codeTraceability.FrontendComponents),
        };
    }

    private static object TraceabilityCategorySummary(IReadOnlyList<TaskCodeTraceabilityItemView> items)
    {
        return new
        {
            itemCount = items.Count,
            targets = items.Select(item => new { item.ChangeType, item.Target }).ToArray(),
        };
    }

    private static string NormalizeRole(string role)
    {
        return role == "Owner" ? "owner" : "member";
    }

    private static string NormalizeWorkflowKey(string stateKey)
    {
        return stateKey.Trim().ToLowerInvariant() switch
        {
            "todo" => "Todo",
            "active" => "Active",
            "review" => "Review",
            "done" => "Done",
            _ => stateKey,
        };
    }

    private static string NormalizeLifecycleState(RonFlow.Domain.TaskLifecycleState lifecycleState)
    {
        return lifecycleState switch
        {
            RonFlow.Domain.TaskLifecycleState.ActiveRecord => "active",
            RonFlow.Domain.TaskLifecycleState.Archived => "archived",
            RonFlow.Domain.TaskLifecycleState.Trashed => "trashed",
            _ => lifecycleState.ToString(),
        };
    }

    private static bool IsReadyLeafTask(BoardTaskCardView task)
    {
        return task.Children.Count == 0 && task.CompletionConditionCount > 0 && task.HasEstimatedEffort;
    }

    private static bool IsReadyLeafTask(TaskDetailView task)
    {
        return task.ChildTasks.Count == 0 && task.Subtasks.Count > 0 && task.EstimatedEffort is not null;
    }

    private static string DescribeHatcheryStatus(BoardTaskCardView task)
    {
        if (task.IsInFlow)
        {
            return "in_flow";
        }

        if (task.Children.Count > 0)
        {
            return "parent_not_executable";
        }

        return IsReadyLeafTask(task) ? "ready_leaf_not_in_flow" : "not_ready_leaf";
    }

    private static string DescribeHatcheryStatus(TaskDetailView task)
    {
        if (task.IsInFlow)
        {
            return "in_flow";
        }

        if (task.ChildTasks.Count > 0)
        {
            return "parent_not_executable";
        }

        return IsReadyLeafTask(task) ? "ready_leaf_not_in_flow" : "not_ready_leaf";
    }

    private static IReadOnlyList<string> GetMissingReadyFields(BoardTaskCardView task)
    {
        if (task.Children.Count > 0)
        {
            return ["none_parent_tasks_cannot_enter_flow"];
        }

        var missingFields = new List<string>();
        if (task.CompletionConditionCount == 0)
        {
            missingFields.Add("completion_conditions");
        }

        if (!task.HasEstimatedEffort)
        {
            missingFields.Add("estimated_effort");
        }

        return missingFields.Count == 0 ? ["none"] : missingFields;
    }

    private static IReadOnlyList<string> GetMissingReadyFields(TaskDetailView task)
    {
        if (task.ChildTasks.Count > 0)
        {
            return ["none_parent_tasks_cannot_enter_flow"];
        }

        var missingFields = new List<string>();
        if (task.Subtasks.Count == 0)
        {
            missingFields.Add("completion_conditions");
        }

        if (task.EstimatedEffort is null)
        {
            missingFields.Add("estimated_effort");
        }

        return missingFields.Count == 0 ? ["none"] : missingFields;
    }

    private static IReadOnlyList<object> GetReadyFieldFormats(BoardTaskCardView task)
    {
        return task.Children.Count == 0 && !task.HasEstimatedEffort
            ? [EstimatedEffortReadyFieldFormat()]
            : [];
    }

    private static IReadOnlyList<object> GetReadyFieldFormats(TaskDetailView task)
    {
        return task.ChildTasks.Count == 0 && task.EstimatedEffort is null
            ? [EstimatedEffortReadyFieldFormat()]
            : [];
    }

    private static object EstimatedEffortReadyFieldFormat()
    {
        return new
        {
            name = "estimated_effort",
            path = "optionalFields.estimatedEffort",
            format = new
            {
                value = "<positive integer>",
                unit = "minutes|hours|days",
            },
            example = new { value = 5, unit = "hours" },
        };
    }

    private static IEnumerable<string> BuildTaskDetailNextActions(TaskDetailView task)
    {
        yield return "update_task_detail";

        if (task.Subtasks.Count > 0)
        {
            yield return "check_task_subtask";
            yield return "uncheck_task_subtask";
        }

        if (task.IsInFlow || IsReadyLeafTask(task))
        {
            yield return "move_task_state";
        }

        if (task.IsInFlow)
        {
            yield return "reorder_task";
        }

        yield return "archive_task";
        yield return "trash_task";
    }

    private static IEnumerable<BoardTaskCardView> FlattenTaskTree(IEnumerable<BoardTaskCardView> tasks)
    {
        foreach (var task in tasks)
        {
            yield return task;

            foreach (var child in FlattenTaskTree(task.Children))
            {
                yield return child;
            }
        }
    }
}
