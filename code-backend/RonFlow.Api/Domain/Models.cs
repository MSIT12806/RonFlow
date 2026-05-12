namespace RonFlow.Domain;

public sealed record WorkflowStateModel(string Key, string Label, bool IsInitialState);

public sealed record ActivityTimelineItemModel(string Type, string Message, DateTimeOffset OccurredAt);

public sealed record TaskModel(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    WorkflowStateModel CurrentState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int SortOrder,
    IReadOnlyList<ActivityTimelineItemModel> ActivityTimeline);

public sealed record ProjectModel(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowStateModel> WorkflowStates);

public sealed record ProjectSummaryModel(Guid Id, string Name, DateTimeOffset UpdatedAt);

public sealed record ProjectBoardModel(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<WorkflowStateModel> WorkflowStates,
    IReadOnlyList<TaskModel> Tasks);