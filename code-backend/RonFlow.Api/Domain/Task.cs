namespace RonFlow.Domain;

public sealed class Task
{
    private readonly List<ActivityTimelineItem> activityTimeline;

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
        this.activityTimeline = activityTimeline.ToList();
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public string Title { get; }

    public WorkflowState CurrentState { get; private set; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<ActivityTimelineItem> ActivityTimeline => activityTimeline;

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

    public static Task Rehydrate(
        Guid id,
        Guid projectId,
        string title,
        WorkflowState currentState,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        return new Task(id, projectId, title, currentState, createdAt, completedAt, activityTimeline);
    }

    public bool ChangeState(WorkflowState targetState, DateTimeOffset changedAt)
    {
        if (CurrentState.Key == targetState.Key)
        {
            return false;
        }

        CurrentState = targetState;
        activityTimeline.Add(ActivityTimelineItem.TaskStateChanged(targetState.Label, changedAt));

        if (targetState.Key == "done" && CompletedAt is null)
        {
            CompletedAt = changedAt;
            activityTimeline.Add(ActivityTimelineItem.TaskCompleted(changedAt));
        }

        return true;
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