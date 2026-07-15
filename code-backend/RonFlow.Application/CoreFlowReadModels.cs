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
    IReadOnlyList<BoardTaskCardView> TaskTree,
    IReadOnlyList<BoardColumnView> Columns);

public sealed record BoardColumnView(
    string StateKey,
    string Label,
    bool IsInitialState,
    bool IsCompletedState,
    string EmptyStateMessage,
    IReadOnlyList<BoardTaskCardView> Tasks);

public sealed record BoardTaskCardView(
    Guid Id,
    string Title,
    bool IsCompleted,
    bool IsInFlow,
    bool IsSplitComplete,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int CompletionConditionCount,
    bool HasEstimatedEffort,
    string ParentPath,
    IReadOnlyList<BoardTaskCardView> Children);

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

public sealed record TaskEstimatedEffortView(int Value, string Unit);

public sealed record TaskDetailView(
    Guid Id,
    Guid ProjectId,
    Guid? ParentTaskId,
    string Title,
    string Description,
    WorkflowStateView CurrentState,
    bool IsInFlow,
    bool IsSplitComplete,
    TaskLifecycleState LifecycleState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    TaskEstimatedEffortView? EstimatedEffort,
    IReadOnlyList<TaskSubtaskView> Subtasks,
    IReadOnlyList<BoardTaskCardView> ChildTasks,
    string ParentPath,
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
        var activeTasks = board.Tasks
            .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
            .ToArray();
        var parentPaths = CreateParentPathLookup(activeTasks);

        var columns = board.WorkflowStates
            .Select(state => new BoardColumnView(
                state.Key,
                state.Label,
                state.IsInitialState,
                state.IsCompletedState,
                "目前沒有任務",
                activeTasks
                    .Where(task => task.IsInFlow)
                    .Where(task => task.CurrentState.Key == state.Key)
                    .OrderBy(task => task.SortOrder)
                    .ThenBy(task => task.CreatedAt)
                    .Select(task => CreateBoardTaskCard(task, parentPaths))
                    .ToArray()))
            .ToArray();

        var taskTreeRoots = activeTasks
            .Where(task => task.IsInFlow is false)
            .Where(task => task.ParentTaskId is null)
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToArray();
        var taskTree = BuildTaskTree(activeTasks, taskTreeRoots, parentPaths);

        return new ProjectBoardView(board.ProjectId, board.ProjectName, taskTree, columns);
    }

    public static TaskDetailView CreateTaskDetail(TaskModel task, IReadOnlyList<TaskModel>? childTasks = null)
    {
        var activeTasks = (childTasks ?? [])
            .Where(childTask => childTask.LifecycleState == TaskLifecycleState.ActiveRecord)
            .ToArray();
        var parentPaths = CreateParentPathLookup(activeTasks);

        return new TaskDetailView(
            task.Id,
            task.ProjectId,
            task.ParentTaskId,
            task.Title,
            task.Description,
            CreateWorkflowState(task.CurrentState),
            task.IsInFlow,
            task.IsSplitComplete,
            task.LifecycleState,
            task.DueDate,
            task.CreatedAt,
            task.CompletedAt,
            task.EstimatedEffort is null ? null : new TaskEstimatedEffortView(task.EstimatedEffort.Value, task.EstimatedEffort.Unit),
            task.Subtasks.Select(CreateTaskSubtask).ToArray(),
            BuildTaskTree(activeTasks, activeTasks.Where(childTask => childTask.ParentTaskId == task.Id).ToArray(), parentPaths),
            parentPaths.GetValueOrDefault(task.Id, string.Empty),
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

    private static BoardTaskCardView CreateBoardTaskCard(
        TaskModel task,
        IReadOnlyDictionary<Guid, string> parentPaths,
        IReadOnlyList<BoardTaskCardView>? children = null)
    {
        return new BoardTaskCardView(
            task.Id,
            task.Title,
            task.CurrentState.IsCompletedState,
            task.IsInFlow,
            task.IsSplitComplete,
            task.CreatedAt,
            task.CompletedAt,
            task.Subtasks.Count,
            task.EstimatedEffort is not null,
            parentPaths.GetValueOrDefault(task.Id, string.Empty),
            children ?? []);
    }

    private static IReadOnlyList<BoardTaskCardView> BuildTaskTree(
        IReadOnlyList<TaskModel> allActiveTasks,
        IReadOnlyList<TaskModel> currentLevelTasks,
        IReadOnlyDictionary<Guid, string> parentPaths)
    {
        return currentLevelTasks
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .Select(task => CreateBoardTaskCard(
                task,
                parentPaths,
                BuildTaskTree(
                    allActiveTasks,
                    allActiveTasks
                        .Where(childTask => childTask.ParentTaskId == task.Id)
                        .OrderBy(childTask => childTask.SortOrder)
                        .ThenBy(childTask => childTask.CreatedAt)
                        .ToArray(),
                    parentPaths)))
            .ToArray();
    }

    private static IReadOnlyDictionary<Guid, string> CreateParentPathLookup(IReadOnlyList<TaskModel> tasks)
    {
        var tasksById = tasks.ToDictionary(task => task.Id);

        return tasks
            .ToDictionary(
                task => task.Id,
                task => string.Join(" > ", GetAncestorTitles(task, tasksById)));
    }

    private static IReadOnlyList<string> GetAncestorTitles(TaskModel task, IReadOnlyDictionary<Guid, TaskModel> tasksById)
    {
        var titles = new Stack<string>();
        var visitedTaskIds = new HashSet<Guid> { task.Id };
        var currentParentTaskId = task.ParentTaskId;

        while (currentParentTaskId is not null
            && tasksById.TryGetValue(currentParentTaskId.Value, out var parentTask)
            && visitedTaskIds.Add(parentTask.Id))
        {
            titles.Push(parentTask.Title);
            currentParentTaskId = parentTask.ParentTaskId;
        }

        return titles.ToArray();
    }

    internal static WorkflowStateView CreateWorkflowState(WorkflowStateModel workflowState)
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
