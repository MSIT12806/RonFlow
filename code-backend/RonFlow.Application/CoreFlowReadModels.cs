using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record ProjectListView(IReadOnlyList<ProjectListItemView> Items);

public sealed record ProjectListItemView(Guid Id, string Name, DateTimeOffset UpdatedAt, string Role);

public sealed record ProjectMemberListView(
    IReadOnlyList<ProjectMemberView> Items,
    IReadOnlyList<ProjectOnlineUserView> OnlineUsers);

public sealed record ProjectMemberView(string UserName, string Role);

public sealed record ProjectOnlineUserView(string UserName);

public sealed record ProjectInvitationListView(IReadOnlyList<ProjectInvitationView> Items);

public sealed record ProjectInvitationView(Guid Id, string Invitee, string Status);

public sealed record InvitationInboxView(IReadOnlyList<InvitationInboxItemView> Items);

public sealed record InvitationInboxItemView(Guid Id, Guid ProjectId, string ProjectName, string InviterName);

public sealed record ProjectView(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowStateView> WorkflowStates);

public sealed record ProjectBoardView(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<BoardColumnView> Columns);

public sealed record BoardColumnView(
    string StateKey,
    string Label,
    bool IsInitialState,
    bool IsCompletedState,
    string EmptyStateMessage,
    IReadOnlyList<BoardTaskCardView> Tasks);

public sealed record BoardTaskCardView(Guid Id, string Title);

public sealed record ProjectCodeTraceabilityView(IReadOnlyList<ProjectCodeTraceabilityItemView> Items);

public sealed record ProjectCodeTraceabilityItemView(
    Guid TaskId,
    string TaskTitle,
    string Category,
    string ChangeType,
    string Target);

public sealed record WorkflowStateView(string Key, string Label, bool IsInitialState, bool IsCompletedState);

public sealed record ProjectSubtaskTemplateView(Guid Id, string Title, int Order);

public sealed record TaskSubtaskView(Guid Id, string Title, bool IsChecked, int Order);

public sealed record TaskCodeTraceabilityItemView(string ChangeType, string Target);

public sealed record TaskCodeTraceabilityView(
    IReadOnlyList<TaskCodeTraceabilityItemView> Api,
    IReadOnlyList<TaskCodeTraceabilityItemView> FrontendPages,
    IReadOnlyList<TaskCodeTraceabilityItemView> FrontendComponents);

public sealed record TaskDetailView(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    WorkflowStateView CurrentState,
    TaskLifecycleState LifecycleState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TaskSubtaskView> Subtasks,
    TaskCodeTraceabilityView CodeTraceability,
    IReadOnlyList<TaskReminderView> Reminders,
    IReadOnlyList<ActivityTimelineItemView> ActivityTimeline);

public sealed record ProjectSubtaskTemplateListView(IReadOnlyList<ProjectSubtaskTemplateView> Items);

public sealed record LifecycleTaskListView(IReadOnlyList<LifecycleTaskListItemView> Items);

public sealed record LifecycleTaskListItemView(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    WorkflowStateView OriginalState,
    DateTimeOffset ChangedAt);

public sealed record ActivityTimelineItemView(string Type, string Message, DateTimeOffset OccurredAt);

public sealed record TaskReminderView(Guid Id, string ReminderDateTime, string Description);

internal static class CoreFlowReadModelFactory
{
    public static ProjectListView CreateProjectList(IReadOnlyList<ProjectSummaryModel> projects)
    {
        return new ProjectListView(projects.Select(CreateProjectListItem).ToArray());
    }

    public static ProjectListView CreateProjectList(IReadOnlyList<ProjectListItemView> projects)
    {
        return new ProjectListView(projects);
    }

    public static ProjectView CreateProject(ProjectModel project)
    {
        return new ProjectView(
            project.Id,
            project.Name,
            project.UpdatedAt,
            project.WorkflowStates.Select(CreateWorkflowState).ToArray());
    }

    public static ProjectSubtaskTemplateListView CreateProjectSubtaskTemplates(ProjectModel project)
    {
        return new ProjectSubtaskTemplateListView(project.SubtaskTemplates.Select(CreateProjectSubtaskTemplate).ToArray());
    }

    public static ProjectBoardView CreateProjectBoard(ProjectBoardModel board)
    {
        var columns = board.WorkflowStates
            .Select(state => new BoardColumnView(
                state.Key,
                state.Label,
                state.IsInitialState,
                state.IsCompletedState,
                "目前沒有任務",
                board.Tasks
                    .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
                    .Where(task => task.CurrentState.Key == state.Key)
                    .Select(CreateBoardTaskCard)
                    .ToArray()))
            .ToArray();

        return new ProjectBoardView(board.ProjectId, board.ProjectName, columns);
    }

    public static TaskDetailView CreateTaskDetail(TaskModel task)
    {
        return new TaskDetailView(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Description,
            CreateWorkflowState(task.CurrentState),
            task.LifecycleState,
            task.DueDate,
            task.CreatedAt,
            task.CompletedAt,
            task.Subtasks.Select(CreateTaskSubtask).ToArray(),
            CreateTaskCodeTraceability(task.CodeTraceability),
            task.Reminders.Select(CreateTaskReminder).ToArray(),
            task.ActivityTimeline.Select(CreateActivityTimelineItem).ToArray());
    }

    public static ProjectCodeTraceabilityView CreateProjectCodeTraceability(ProjectBoardModel board)
    {
        return new ProjectCodeTraceabilityView(
            board.Tasks
                .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
                .SelectMany(CreateProjectCodeTraceabilityItems)
                .ToArray());
    }

    public static LifecycleTaskListView CreateLifecycleTaskList(Project project, IReadOnlyList<TaskModel> tasks, TaskLifecycleState lifecycleState)
    {
        return new LifecycleTaskListView(
            tasks
                .Where(task => task.LifecycleState == lifecycleState)
                .OrderByDescending(task => GetLifecycleChangedAt(task, lifecycleState))
                .Select(task => new LifecycleTaskListItemView(
                    task.Id,
                    task.ProjectId,
                    project.Name,
                    task.Title,
                    CreateWorkflowState(task.CurrentState),
                    GetLifecycleChangedAt(task, lifecycleState)))
                .ToArray());
    }

    private static ProjectListItemView CreateProjectListItem(ProjectSummaryModel project)
    {
        return new ProjectListItemView(project.Id, project.Name, project.UpdatedAt, "專案擁有者");
    }

    private static BoardTaskCardView CreateBoardTaskCard(TaskModel task)
    {
        return new BoardTaskCardView(task.Id, task.Title);
    }

    private static WorkflowStateView CreateWorkflowState(WorkflowStateModel workflowState)
    {
        return new WorkflowStateView(workflowState.Key, workflowState.Label, workflowState.IsInitialState, workflowState.IsCompletedState);
    }

    private static ActivityTimelineItemView CreateActivityTimelineItem(ActivityTimelineItemModel activityTimelineItem)
    {
        return new ActivityTimelineItemView(activityTimelineItem.Type, activityTimelineItem.Message, activityTimelineItem.OccurredAt);
    }

    private static ProjectSubtaskTemplateView CreateProjectSubtaskTemplate(ProjectSubtaskTemplateModel template)
    {
        return new ProjectSubtaskTemplateView(template.Id, template.Title, template.Order);
    }

    private static TaskSubtaskView CreateTaskSubtask(TaskSubtaskModel subtask)
    {
        return new TaskSubtaskView(subtask.Id, subtask.Title, subtask.IsChecked, subtask.Order);
    }

    private static TaskCodeTraceabilityView CreateTaskCodeTraceability(TaskCodeTraceabilityModel codeTraceability)
    {
        return new(
            codeTraceability.Api.Select(CreateTaskCodeTraceabilityItem).ToArray(),
            codeTraceability.FrontendPages.Select(CreateTaskCodeTraceabilityItem).ToArray(),
            codeTraceability.FrontendComponents.Select(CreateTaskCodeTraceabilityItem).ToArray());
    }

    private static TaskCodeTraceabilityItemView CreateTaskCodeTraceabilityItem(TaskCodeTraceabilityItemModel item)
    {
        return new(item.ChangeType, item.Target);
    }

    private static IEnumerable<ProjectCodeTraceabilityItemView> CreateProjectCodeTraceabilityItems(TaskModel task)
    {
        return task.CodeTraceability.Api.Select(item => CreateProjectCodeTraceabilityItem(task, "api", item))
            .Concat(task.CodeTraceability.FrontendPages.Select(item => CreateProjectCodeTraceabilityItem(task, "frontendPages", item)))
            .Concat(task.CodeTraceability.FrontendComponents.Select(item => CreateProjectCodeTraceabilityItem(task, "frontendComponents", item)));
    }

    private static ProjectCodeTraceabilityItemView CreateProjectCodeTraceabilityItem(
        TaskModel task,
        string category,
        TaskCodeTraceabilityItemModel item)
    {
        return new(task.Id, task.Title, category, item.ChangeType, item.Target);
    }

    private static TaskReminderView CreateTaskReminder(TaskReminderModel reminder)
    {
        return new TaskReminderView(reminder.Id, reminder.ReminderDateTime, reminder.Description);
    }

    private static DateTimeOffset GetLifecycleChangedAt(TaskModel task, TaskLifecycleState lifecycleState)
    {
        return lifecycleState switch
        {
            TaskLifecycleState.Archived => task.ArchivedAt ?? task.CreatedAt,
            TaskLifecycleState.Trashed => task.TrashedAt ?? task.CreatedAt,
            _ => task.CreatedAt,
        };
    }
}