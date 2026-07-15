namespace RonFlow.Domain;

public enum TaskLifecycleState
{
    ActiveRecord,
    Archived,
    Trashed,
}

public sealed record WorkflowStateModel(string Key, string Label, bool IsInitialState, bool IsCompletedState);

public sealed record ActivityTimelineItemModel(string Type, string Message, DateTimeOffset OccurredAt);

public sealed record TaskReminderModel(Guid Id, string ReminderDateTime, string Description);

public sealed record TaskCodeTraceabilityItemModel(string ChangeType, string Target);

public sealed record TaskCodeTraceabilityModel(
    IReadOnlyList<TaskCodeTraceabilityItemModel> Api,
    IReadOnlyList<TaskCodeTraceabilityItemModel> FrontendPages,
    IReadOnlyList<TaskCodeTraceabilityItemModel> FrontendComponents);

public sealed record TaskEstimatedEffortModel(int Value, string Unit);

public sealed record ProjectSubtaskTemplateModel(Guid Id, string Title, int Order);

public sealed record TaskSubtaskModel(Guid Id, string Title, bool IsChecked, int Order);

public sealed record TaskModel(
    Guid Id,
    Guid ProjectId,
    Guid? ParentTaskId,
    string Title,
    string Description,
    WorkflowStateModel CurrentState,
    bool IsInFlow,
    bool IsSplitComplete,
    bool IsShort,
    TaskLifecycleState LifecycleState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset MutationAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? TrashedAt,
    int SortOrder,
    TaskEstimatedEffortModel? EstimatedEffort,
    IReadOnlyList<TaskSubtaskModel> Subtasks,
    IReadOnlyList<TaskReminderModel> Reminders,
    TaskCodeTraceabilityModel CodeTraceability,
    IReadOnlyList<ActivityTimelineItemModel> ActivityTimeline);

public sealed record ProjectModel(
    Guid Id,
    Guid OwnerId,
    string Name,
    DateTimeOffset UpdatedAt,
    DateTimeOffset MutationAt,
    IReadOnlyList<ProjectSubtaskTemplateModel> SubtaskTemplates,
    IReadOnlyList<WorkflowStateModel> WorkflowStates);

public sealed record ProjectSummaryModel(Guid Id, Guid OwnerId, string Name, DateTimeOffset UpdatedAt, DateTimeOffset MutationAt);

public sealed record ProjectBoardModel(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<WorkflowStateModel> WorkflowStates,
    IReadOnlyList<TaskModel> Tasks);
