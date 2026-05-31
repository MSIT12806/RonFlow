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
    TaskLifecycleState LifecycleState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TaskSubtaskOutput> Subtasks,
    TaskCodeTraceabilityOutput CodeTraceability,
    IReadOnlyList<CreatedTaskReminderOutput> Reminders,
    IReadOnlyList<CreatedActivityTimelineItemOutput> ActivityTimeline);

public sealed record CreatedTaskReminderOutput(Guid Id, string ReminderDateTime, string Description);

public sealed record TaskCodeTraceabilityOutput(
    IReadOnlyList<TaskCodeTraceabilityItemOutput> Api,
    IReadOnlyList<TaskCodeTraceabilityItemOutput> FrontendPages,
    IReadOnlyList<TaskCodeTraceabilityItemOutput> FrontendComponents);

public sealed record TaskCodeTraceabilityItemOutput(string ChangeType, string Target);

public sealed record ProjectSubtaskTemplateOutput(Guid Id, string Title, int Order);

public sealed record TaskSubtaskOutput(Guid Id, string Title, bool IsChecked, int Order);

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
            task.LifecycleState,
            task.DueDate,
            task.CreatedAt,
            task.CompletedAt,
            task.Subtasks.Select(CreateTaskSubtask).ToArray(),
            CreateTaskCodeTraceability(task.CodeTraceability),
            task.Reminders.Select(CreateTaskReminder).ToArray(),
            task.ActivityTimeline.Select(CreateActivityTimelineItem).ToArray());
    }

    public static IReadOnlyList<ProjectSubtaskTemplateOutput> CreateProjectSubtaskTemplates(ProjectModel project)
    {
        return project.SubtaskTemplates.Select(template => new ProjectSubtaskTemplateOutput(template.Id, template.Title, template.Order)).ToArray();
    }

    private static CreatedTaskReminderOutput CreateTaskReminder(TaskReminderModel reminder)
    {
        return new CreatedTaskReminderOutput(reminder.Id, reminder.ReminderDateTime, reminder.Description);
    }

    private static TaskSubtaskOutput CreateTaskSubtask(TaskSubtaskModel subtask)
    {
        return new TaskSubtaskOutput(subtask.Id, subtask.Title, subtask.IsChecked, subtask.Order);
    }

    private static TaskCodeTraceabilityOutput CreateTaskCodeTraceability(TaskCodeTraceabilityModel codeTraceability)
    {
        return new(
            codeTraceability.Api.Select(CreateTaskCodeTraceabilityItem).ToArray(),
            codeTraceability.FrontendPages.Select(CreateTaskCodeTraceabilityItem).ToArray(),
            codeTraceability.FrontendComponents.Select(CreateTaskCodeTraceabilityItem).ToArray());
    }

    private static TaskCodeTraceabilityItemOutput CreateTaskCodeTraceabilityItem(TaskCodeTraceabilityItemModel item)
    {
        return new(item.ChangeType, item.Target);
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