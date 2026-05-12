namespace RonFlow.Domain;

public sealed class Task
{
    private readonly List<ActivityTimelineItem> activityTimeline;

    private Task(
        Guid id,
        Guid projectId,
        string title,
        string description,
        WorkflowState currentState,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        int sortOrder,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        Id = id;
        ProjectId = projectId;
        Title = title;
        Description = description;
        CurrentState = currentState;
        DueDate = dueDate;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
        SortOrder = sortOrder;
        this.activityTimeline = activityTimeline.ToList();
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public WorkflowState CurrentState { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyList<ActivityTimelineItem> ActivityTimeline => activityTimeline;

    public static Task Create(Guid projectId, TaskTitle title, WorkflowState initialState, DateTimeOffset createdAt, int sortOrder)
    {
        return new Task(
            Guid.NewGuid(),
            projectId,
            title.Value,
            string.Empty,
            initialState,
            null,
            createdAt,
            null,
            sortOrder,
            [ActivityTimelineItem.TaskCreated(createdAt)]);
    }

    public static Task Rehydrate(
        Guid id,
        Guid projectId,
        string title,
        string description,
        WorkflowState currentState,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        int sortOrder,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        return new Task(id, projectId, title, description, currentState, dueDate, createdAt, completedAt, sortOrder, activityTimeline);
    }

    public bool ChangeState(WorkflowState targetState, DateTimeOffset changedAt)
    {
        if (CurrentState.Key == targetState.Key)
        {
            return false;
        }

        var wasDone = CurrentState.Key == "done";
        var isDone = targetState.Key == "done";

        CurrentState = targetState;
        activityTimeline.Add(ActivityTimelineItem.TaskStateChanged(targetState.Label, changedAt));

        if (!wasDone && isDone)
        {
            CompletedAt = changedAt;
            activityTimeline.Add(ActivityTimelineItem.TaskCompleted(changedAt));
        }

        if (wasDone && !isDone)
        {
            CompletedAt = null;
            activityTimeline.Add(ActivityTimelineItem.TaskReopened(changedAt));
        }

        return true;
    }

    public bool UpdateDetails(TaskTitle title, string description, DateOnly? dueDate, DateTimeOffset changedAt)
    {
        var hasChanged = false;

        if (Title != title.Value)
        {
            Title = title.Value;
            activityTimeline.Add(ActivityTimelineItem.TaskTitleChanged(changedAt));
            hasChanged = true;
        }

        if (Description != description)
        {
            Description = description;
            activityTimeline.Add(ActivityTimelineItem.TaskDescriptionChanged(changedAt));
            hasChanged = true;
        }

        if (DueDate != dueDate)
        {
            DueDate = dueDate;
            activityTimeline.Add(ActivityTimelineItem.TaskDueDateChanged(dueDate, changedAt));
            hasChanged = true;
        }

        return hasChanged;
    }

    public bool UpdateSortOrder(int sortOrder, DateTimeOffset changedAt, bool recordActivity)
    {
        var hasChanged = SortOrder != sortOrder;
        SortOrder = sortOrder;

        if (recordActivity)
        {
            activityTimeline.Add(ActivityTimelineItem.TaskReordered(changedAt));
        }

        return true;
    }

    public TaskModel ToModel()
    {
        return new TaskModel(
            Id,
            ProjectId,
            Title,
            Description,
            CurrentState.ToModel(),
            DueDate,
            CreatedAt,
            CompletedAt,
            SortOrder,
            activityTimeline.Select(item => item.ToModel()).ToArray());
    }
}