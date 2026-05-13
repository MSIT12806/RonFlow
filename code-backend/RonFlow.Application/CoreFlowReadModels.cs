using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record ProjectListView(IReadOnlyList<ProjectListItemView> Items);

public sealed record ProjectListItemView(Guid Id, string Name, DateTimeOffset UpdatedAt);

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
    string EmptyStateMessage,
    IReadOnlyList<BoardTaskCardView> Tasks);

public sealed record BoardTaskCardView(Guid Id, string Title);

public sealed record WorkflowStateView(string Key, string Label, bool IsInitialState);

public sealed record TaskDetailView(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    WorkflowStateView CurrentState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ActivityTimelineItemView> ActivityTimeline);

public sealed record ActivityTimelineItemView(string Type, string Message, DateTimeOffset OccurredAt);

internal static class CoreFlowReadModelFactory
{
    public static ProjectListView CreateProjectList(IReadOnlyList<ProjectSummaryModel> projects)
    {
        return new ProjectListView(projects.Select(CreateProjectListItem).ToArray());
    }

    public static ProjectView CreateProject(ProjectModel project)
    {
        return new ProjectView(
            project.Id,
            project.Name,
            project.UpdatedAt,
            project.WorkflowStates.Select(CreateWorkflowState).ToArray());
    }

    public static ProjectBoardView CreateProjectBoard(ProjectBoardModel board)
    {
        var columns = board.WorkflowStates
            .Select(state => new BoardColumnView(
                state.Key,
                state.Label,
                state.IsInitialState,
                "目前沒有任務",
                board.Tasks
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
            task.DueDate,
            task.CreatedAt,
            task.CompletedAt,
            task.ActivityTimeline.Select(CreateActivityTimelineItem).ToArray());
    }

    private static ProjectListItemView CreateProjectListItem(ProjectSummaryModel project)
    {
        return new ProjectListItemView(project.Id, project.Name, project.UpdatedAt);
    }

    private static BoardTaskCardView CreateBoardTaskCard(TaskModel task)
    {
        return new BoardTaskCardView(task.Id, task.Title);
    }

    private static WorkflowStateView CreateWorkflowState(WorkflowStateModel workflowState)
    {
        return new WorkflowStateView(workflowState.Key, workflowState.Label, workflowState.IsInitialState);
    }

    private static ActivityTimelineItemView CreateActivityTimelineItem(ActivityTimelineItemModel activityTimelineItem)
    {
        return new ActivityTimelineItemView(activityTimelineItem.Type, activityTimelineItem.Message, activityTimelineItem.OccurredAt);
    }
}