namespace RonFlow.Domain;

public sealed class Task
{
    private readonly List<ActivityTimelineItem> activityTimeline;
    private readonly List<TaskReminder> reminders;

    private Task(
        Guid id,
        Guid projectId,
        string title,
        string description,
        WorkflowState currentState,
        TaskLifecycleState lifecycleState,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? archivedAt,
        DateTimeOffset? trashedAt,
        int sortOrder,
        IEnumerable<TaskReminder> reminders,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        Id = id;
        ProjectId = projectId;
        Title = title;
        Description = description;
        CurrentState = currentState;
        LifecycleState = lifecycleState;
        DueDate = dueDate;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
        ArchivedAt = archivedAt;
        TrashedAt = trashedAt;
        SortOrder = sortOrder;
        this.reminders = reminders.ToList();
        this.activityTimeline = activityTimeline.ToList();
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public WorkflowState CurrentState { get; private set; }

    public TaskLifecycleState LifecycleState { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public DateTimeOffset? TrashedAt { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyList<TaskReminder> Reminders => reminders;

    public IReadOnlyList<ActivityTimelineItem> ActivityTimeline => activityTimeline;

    public static Task Create(Guid projectId, TaskTitle title, WorkflowState initialState, DateTimeOffset createdAt, int sortOrder)
    {
        return new Task(
            Guid.NewGuid(),
            projectId,
            title.Value,
            string.Empty,
            initialState,
                TaskLifecycleState.ActiveRecord,
            null,
            createdAt,
            null,
                null,
                null,
            sortOrder,
            [],
            [ActivityTimelineItem.TaskCreated(createdAt)]);
    }

    public static Task Rehydrate(
        Guid id,
        Guid projectId,
        string title,
        string description,
        WorkflowState currentState,
        TaskLifecycleState lifecycleState,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? archivedAt,
        DateTimeOffset? trashedAt,
        int sortOrder,
        IEnumerable<TaskReminder> reminders,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        return new Task(id, projectId, title, description, currentState, lifecycleState, dueDate, createdAt, completedAt, archivedAt, trashedAt, sortOrder, reminders, activityTimeline);
    }

    public bool AddReminder(string reminderDateTime, string description, DateTimeOffset changedAt)
    {
        var normalizedDateTime = reminderDateTime.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDateTime))
        {
            return false;
        }

        reminders.Add(new TaskReminder(Guid.NewGuid(), normalizedDateTime, description.Trim()));
        activityTimeline.Add(ActivityTimelineItem.TaskReminderAdded(changedAt));
        return true;
    }

    public bool DeleteReminder(Guid reminderId, DateTimeOffset changedAt)
    {
        var removedCount = reminders.RemoveAll(reminder => reminder.Id == reminderId);
        if (removedCount == 0)
        {
            return false;
        }

        activityTimeline.Add(ActivityTimelineItem.TaskReminderDeleted(changedAt));
        return true;
    }

    public bool Archive(DateTimeOffset changedAt)
    {
        if (LifecycleState == TaskLifecycleState.Archived)
        {
            return false;
        }

        LifecycleState = TaskLifecycleState.Archived;
        ArchivedAt = changedAt;
        TrashedAt = null;
        activityTimeline.Add(ActivityTimelineItem.TaskArchived(changedAt));
        return true;
    }

    public bool MoveToTrash(DateTimeOffset changedAt)
    {
        if (LifecycleState == TaskLifecycleState.Trashed)
        {
            return false;
        }

        LifecycleState = TaskLifecycleState.Trashed;
        TrashedAt = changedAt;
        ArchivedAt = null;
        activityTimeline.Add(ActivityTimelineItem.TaskMovedToTrash(changedAt));
        return true;
    }

    public bool RestoreFromArchive(int sortOrder, DateTimeOffset changedAt)
    {
        if (LifecycleState != TaskLifecycleState.Archived)
        {
            return false;
        }

        LifecycleState = TaskLifecycleState.ActiveRecord;
        ArchivedAt = null;
        SortOrder = sortOrder;
        activityTimeline.Add(ActivityTimelineItem.TaskRestoredFromArchive(changedAt));
        return true;
    }

    public bool RestoreFromTrash(int sortOrder, DateTimeOffset changedAt)
    {
        if (LifecycleState != TaskLifecycleState.Trashed)
        {
            return false;
        }

        LifecycleState = TaskLifecycleState.ActiveRecord;
        TrashedAt = null;
        SortOrder = sortOrder;
        activityTimeline.Add(ActivityTimelineItem.TaskRestoredFromTrash(changedAt));
        return true;
    }

    public bool ChangeState(WorkflowState targetState, DateTimeOffset changedAt)
    {
        if (CurrentState.Key == targetState.Key)
        {
            return false;
        }

        var wasDone = CurrentState.IsCompletedState;
        var isDone = targetState.IsCompletedState;

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
            LifecycleState,
            DueDate,
            CreatedAt,
            CompletedAt,
            ArchivedAt,
            TrashedAt,
            SortOrder,
            reminders.Select(reminder => reminder.ToModel()).ToArray(),
            activityTimeline.Select(item => item.ToModel()).ToArray());
    }
}