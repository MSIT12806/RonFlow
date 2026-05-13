using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record CreateProjectOutput(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CreatedWorkflowStateOutput> WorkflowStates);

public sealed record CreatedWorkflowStateOutput(string Key, string Label, bool IsInitialState, bool IsCompletedState);

public sealed record CreateTaskOutput(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    CreatedWorkflowStateOutput CurrentState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<CreatedActivityTimelineItemOutput> ActivityTimeline);

public sealed record CreatedActivityTimelineItemOutput(string Type, string Message, DateTimeOffset OccurredAt);

internal static class CoreFlowCommandOutputFactory
{
    public static CreateProjectOutput CreateProject(ProjectModel project)
    {
        return new CreateProjectOutput(
            project.Id,
            project.Name,
            project.UpdatedAt,
            project.WorkflowStates.Select(CreateWorkflowState).ToArray());
    }

    public static CreateTaskOutput CreateTask(TaskModel task)
    {
        return new CreateTaskOutput(
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

    private static CreatedWorkflowStateOutput CreateWorkflowState(WorkflowStateModel workflowState)
    {
        return new CreatedWorkflowStateOutput(workflowState.Key, workflowState.Label, workflowState.IsInitialState, workflowState.IsCompletedState);
    }

    private static CreatedActivityTimelineItemOutput CreateActivityTimelineItem(ActivityTimelineItemModel activityTimelineItem)
    {
        return new CreatedActivityTimelineItemOutput(activityTimelineItem.Type, activityTimelineItem.Message, activityTimelineItem.OccurredAt);
    }
}