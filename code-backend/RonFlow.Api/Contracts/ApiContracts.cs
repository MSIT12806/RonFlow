using RonFlow.Application;

namespace RonFlow.Api.Contracts;

public sealed record CreateProjectRequest(string? Name);

public sealed record CreateTaskRequest(string? Title);

public sealed record ChangeTaskStateRequest(string? StateKey);

public sealed record UpdateTaskRequest(string? Title, string? Description, DateOnly? DueDate);

public sealed record ReorderTaskRequest(Guid? TargetTaskId);

public sealed record ProjectListResponse(IReadOnlyList<ProjectListItemResponse> Items);

public sealed record ProjectListItemResponse(Guid Id, string Name, DateTimeOffset UpdatedAt)
{
    public static ProjectListItemResponse FromView(ProjectListItemView view)
    {
        return new(view.Id, view.Name, view.UpdatedAt);
    }
}

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowStateResponse> WorkflowStates)
{
    public static ProjectResponse FromOutput(CreateProjectOutput output)
    {
        return new(
            output.Id,
            output.Name,
            output.UpdatedAt,
            output.WorkflowStates.Select(WorkflowStateResponse.FromOutput).ToArray());
    }

    public static ProjectResponse FromView(ProjectView view)
    {
        return new(
            view.Id,
            view.Name,
            view.UpdatedAt,
            view.WorkflowStates.Select(WorkflowStateResponse.FromView).ToArray());
    }
}

public sealed record ProjectBoardResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<BoardColumnResponse> Columns)
{
    public static ProjectBoardResponse FromView(ProjectBoardView view)
    {
        var columns = view.Columns.Select(BoardColumnResponse.FromView).ToArray();

        return new(view.ProjectId, view.ProjectName, columns);
    }
}

public sealed record BoardColumnResponse(
    string StateKey,
    string Label,
    bool IsInitialState,
    string EmptyStateMessage,
    IReadOnlyList<BoardTaskCardResponse> Tasks)
{
    public static BoardColumnResponse FromView(BoardColumnView view)
    {
        return new(
            view.StateKey,
            view.Label,
            view.IsInitialState,
            view.EmptyStateMessage,
            view.Tasks.Select(BoardTaskCardResponse.FromView).ToArray());
    }
}

public sealed record BoardTaskCardResponse(Guid Id, string Title)
{
    public static BoardTaskCardResponse FromView(BoardTaskCardView view)
    {
        return new(view.Id, view.Title);
    }
}

public sealed record WorkflowStateResponse(string Key, string Label, bool IsInitialState)
{
    public static WorkflowStateResponse FromOutput(CreatedWorkflowStateOutput output)
    {
        return new(output.Key, output.Label, output.IsInitialState);
    }

    public static WorkflowStateResponse FromView(WorkflowStateView view)
    {
        return new(view.Key, view.Label, view.IsInitialState);
    }
}

public sealed record TaskDetailResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    WorkflowStateResponse CurrentState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ActivityTimelineItemResponse> ActivityTimeline)
{
    public static TaskDetailResponse FromOutput(CreateTaskOutput output)
    {
        return new(
            output.Id,
            output.ProjectId,
            output.Title,
            output.Description,
            WorkflowStateResponse.FromOutput(output.CurrentState),
            output.DueDate,
            output.CreatedAt,
            output.CompletedAt,
            output.ActivityTimeline.Select(ActivityTimelineItemResponse.FromOutput).ToArray());
    }

    public static TaskDetailResponse FromView(TaskDetailView view)
    {
        return new(
            view.Id,
            view.ProjectId,
            view.Title,
            view.Description,
            WorkflowStateResponse.FromView(view.CurrentState),
            view.DueDate,
            view.CreatedAt,
            view.CompletedAt,
            view.ActivityTimeline.Select(ActivityTimelineItemResponse.FromView).ToArray());
    }
}

public sealed record ActivityTimelineItemResponse(string Type, string Message, DateTimeOffset OccurredAt)
{
    public static ActivityTimelineItemResponse FromOutput(CreatedActivityTimelineItemOutput output)
    {
        return new(output.Type, output.Message, output.OccurredAt);
    }

    public static ActivityTimelineItemResponse FromView(ActivityTimelineItemView view)
    {
        return new(view.Type, view.Message, view.OccurredAt);
    }
}