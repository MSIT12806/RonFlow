namespace RonFlow.Domain;

public sealed class Task
{
    private readonly IReadOnlyList<ActivityTimelineItem> activityTimeline;

    private Task(
        Guid id,
        Guid projectId,
        string title,
        WorkflowState currentState,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        Id = id;
        ProjectId = projectId;
        Title = title;
        CurrentState = currentState;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
        this.activityTimeline = activityTimeline.ToArray();
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public string Title { get; }

    public WorkflowState CurrentState { get; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CompletedAt { get; }

    public static Task Create(Guid projectId, TaskTitle title, WorkflowState initialState, DateTimeOffset createdAt)
    {
        return new Task(
            Guid.NewGuid(),
            projectId,
            title.Value,
            initialState,
            createdAt,
            null,
            [ActivityTimelineItem.TaskCreated(createdAt)]);
    }

    public TaskModel ToModel()
    {
        return new TaskModel(
            Id,
            ProjectId,
            Title,
            CurrentState.ToModel(),
            CreatedAt,
            CompletedAt,
            activityTimeline.Select(item => item.ToModel()).ToArray());
    }
}