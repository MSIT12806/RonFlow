using RonFlow.Api.Domain;

namespace RonFlow.Api.Contracts;

public sealed record CreateProjectRequest(string? Name);

public sealed record CreateTaskRequest(string? Title);

public sealed record ProjectListResponse(IReadOnlyList<ProjectListItemResponse> Items);

public sealed record ProjectListItemResponse(Guid Id, string Name, DateTimeOffset UpdatedAt)
{
    public static ProjectListItemResponse FromModel(ProjectSummaryModel model)
    {
        return new(model.Id, model.Name, model.UpdatedAt);
    }
}

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowStateResponse> WorkflowStates)
{
    public static ProjectResponse FromModel(ProjectModel model)
    {
        return new(
            model.Id,
            model.Name,
            model.UpdatedAt,
            model.WorkflowStates.Select(WorkflowStateResponse.FromModel).ToArray());
    }
}

public sealed record ProjectBoardResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<BoardColumnResponse> Columns)
{
    public static ProjectBoardResponse FromModel(ProjectBoardModel model)
    {
        var columns = model.WorkflowStates
            .Select(state => new BoardColumnResponse(
                state.Key,
                state.Label,
                state.IsInitialState,
                "目前沒有任務",
                model.Tasks
                    .Where(task => task.CurrentState.Key == state.Key)
                    .Select(BoardTaskCardResponse.FromModel)
                    .ToArray()))
            .ToArray();

        return new(model.ProjectId, model.ProjectName, columns);
    }
}

public sealed record BoardColumnResponse(
    string StateKey,
    string Label,
    bool IsInitialState,
    string EmptyStateMessage,
    IReadOnlyList<BoardTaskCardResponse> Tasks);

public sealed record BoardTaskCardResponse(Guid Id, string Title)
{
    public static BoardTaskCardResponse FromModel(TaskModel model)
    {
        return new(model.Id, model.Title);
    }
}

public sealed record WorkflowStateResponse(string Key, string Label, bool IsInitialState)
{
    public static WorkflowStateResponse FromModel(WorkflowStateModel model)
    {
        return new(model.Key, model.Label, model.IsInitialState);
    }
}

public sealed record TaskDetailResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    WorkflowStateResponse CurrentState,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ActivityTimelineItemResponse> ActivityTimeline)
{
    public static TaskDetailResponse FromModel(TaskModel model)
    {
        return new(
            model.Id,
            model.ProjectId,
            model.Title,
            WorkflowStateResponse.FromModel(model.CurrentState),
            model.CreatedAt,
            model.ActivityTimeline.Select(ActivityTimelineItemResponse.FromModel).ToArray());
    }
}

public sealed record ActivityTimelineItemResponse(string Type, string Message, DateTimeOffset OccurredAt)
{
    public static ActivityTimelineItemResponse FromModel(ActivityTimelineItemModel model)
    {
        return new(model.Type, model.Message, model.OccurredAt);
    }
}