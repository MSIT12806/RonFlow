namespace RonFlow.Domain;

/// <summary>
/// 表示 RonFlow 的任務聚合根。
/// </summary>
public sealed class Task
{
    private readonly List<ActivityTimelineItem> activityTimeline;
    private readonly List<TaskReminder> reminders;
    private readonly List<TaskSubtask> subtasks;
    private TaskCodeTraceability codeTraceability;

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
        IEnumerable<TaskSubtask> subtasks,
        IEnumerable<TaskReminder> reminders,
        TaskCodeTraceability codeTraceability,
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
        this.subtasks = subtasks.OrderBy(subtask => subtask.Order).ToList();
        this.reminders = reminders.ToList();
        this.codeTraceability = codeTraceability;
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

    public IReadOnlyList<TaskSubtask> Subtasks => subtasks;

    public IReadOnlyList<TaskReminder> Reminders => reminders;

    public TaskCodeTraceability CodeTraceability => codeTraceability;

    public IReadOnlyList<ActivityTimelineItem> ActivityTimeline => activityTimeline;

    public static Task Create(
        Guid projectId,
        TaskTitle title,
        WorkflowState initialState,
        DateTimeOffset createdAt,
        int sortOrder,
        IEnumerable<TaskSubtask>? subtasks = null)
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
            subtasks ?? [],
            [],
                TaskCodeTraceability.Empty,
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
        IEnumerable<TaskSubtask> subtasks,
        IEnumerable<TaskReminder> reminders,
        TaskCodeTraceability codeTraceability,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        return new Task(id, projectId, title, description, currentState, lifecycleState, dueDate, createdAt, completedAt, archivedAt, trashedAt, sortOrder, subtasks, reminders, codeTraceability, activityTimeline);
    }

    /// <summary>
    /// 在任務上新增一筆提醒。
    /// </summary>
    public TaskMutationExecutionResult AddReminder(TaskMutationAuthorization authorization, string reminderDateTime, string description, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.CreateReminder, out var lockedResult))
        {
            return lockedResult;
        }

        var normalizedDateTime = reminderDateTime.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDateTime))
        {
            return TaskMutationExecutionResult.NoChanges();
        }

        reminders.Add(new TaskReminder(Guid.NewGuid(), normalizedDateTime, description.Trim()));
        activityTimeline.Add(ActivityTimelineItem.TaskReminderAdded(changedAt));
        return TaskMutationExecutionResult.ChangedResult();
    }

    public TaskDeleteReminderExecutionResult DeleteReminder(TaskMutationAuthorization authorization, Guid reminderId, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.DeleteReminder, out _))
        {
            return TaskDeleteReminderExecutionResult.LockedResult();
        }

        var removedCount = reminders.RemoveAll(reminder => reminder.Id == reminderId);
        if (removedCount == 0)
        {
            return TaskDeleteReminderExecutionResult.ReminderMissing();
        }

        activityTimeline.Add(ActivityTimelineItem.TaskReminderDeleted(changedAt));
        return TaskDeleteReminderExecutionResult.ChangedResult();
    }

    public IReadOnlyList<TaskReminder> GetDueUndispatchedReminders(DateTimeOffset currentTime)
    {
        return reminders
            .Where(reminder => reminder.IsDue(currentTime))
            .ToArray();
    }

    public bool MarkReminderNotificationDispatched(Guid reminderId, DateTimeOffset dispatchedAt)
    {
        for (var index = 0; index < reminders.Count; index += 1)
        {
            if (reminders[index].Id != reminderId)
            {
                continue;
            }

            if (reminders[index].NotificationDispatchedAt is not null)
            {
                return false;
            }

            reminders[index] = reminders[index].MarkNotificationDispatched(dispatchedAt);
            return true;
        }

        return false;
    }

    public TaskMutationExecutionResult Archive(TaskMutationAuthorization authorization, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.Archive, out var lockedResult))
        {
            return lockedResult;
        }

        if (LifecycleState == TaskLifecycleState.Archived)
        {
            return TaskMutationExecutionResult.NoChanges();
        }

        LifecycleState = TaskLifecycleState.Archived;
        ArchivedAt = changedAt;
        TrashedAt = null;
        activityTimeline.Add(ActivityTimelineItem.TaskArchived(changedAt));
        return TaskMutationExecutionResult.ChangedResult();
    }

    public TaskMutationExecutionResult MoveToTrash(TaskMutationAuthorization authorization, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.MoveToTrash, out var lockedResult))
        {
            return lockedResult;
        }

        if (LifecycleState == TaskLifecycleState.Trashed)
        {
            return TaskMutationExecutionResult.NoChanges();
        }

        LifecycleState = TaskLifecycleState.Trashed;
        TrashedAt = changedAt;
        ArchivedAt = null;
        activityTimeline.Add(ActivityTimelineItem.TaskMovedToTrash(changedAt));
        return TaskMutationExecutionResult.ChangedResult();
    }

    public TaskMutationExecutionResult RestoreFromArchive(TaskMutationAuthorization authorization, int sortOrder, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.RestoreFromArchive, out var lockedResult))
        {
            return lockedResult;
        }

        if (LifecycleState != TaskLifecycleState.Archived)
        {
            return TaskMutationExecutionResult.NoChanges();
        }

        LifecycleState = TaskLifecycleState.ActiveRecord;
        ArchivedAt = null;
        SortOrder = sortOrder;
        activityTimeline.Add(ActivityTimelineItem.TaskRestoredFromArchive(changedAt));
        return TaskMutationExecutionResult.ChangedResult();
    }

    public TaskMutationExecutionResult RestoreFromTrash(TaskMutationAuthorization authorization, int sortOrder, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.RestoreFromTrash, out var lockedResult))
        {
            return lockedResult;
        }

        if (LifecycleState != TaskLifecycleState.Trashed)
        {
            return TaskMutationExecutionResult.NoChanges();
        }

        LifecycleState = TaskLifecycleState.ActiveRecord;
        TrashedAt = null;
        SortOrder = sortOrder;
        activityTimeline.Add(ActivityTimelineItem.TaskRestoredFromTrash(changedAt));
        return TaskMutationExecutionResult.ChangedResult();
    }

    public TaskMutationExecutionResult ChangeState(TaskMutationAuthorization authorization, WorkflowState targetState, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.ChangeWorkflowState, out var lockedResult))
        {
            return lockedResult;
        }

        if (CurrentState.Key == targetState.Key)
        {
            return TaskMutationExecutionResult.NoChanges();
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

        return TaskMutationExecutionResult.ChangedResult();
    }

    public TaskMutationExecutionResult UpdateDetails(TaskMutationAuthorization authorization, TaskTitle title, string description, DateOnly? dueDate, TaskCodeTraceability? nextCodeTraceability, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.UpdateDetails, out var lockedResult))
        {
            return lockedResult;
        }

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

        if (nextCodeTraceability is not null && codeTraceability != nextCodeTraceability)
        {
            codeTraceability = nextCodeTraceability;
            activityTimeline.Add(ActivityTimelineItem.TaskCodeTraceabilityChanged(changedAt));
            hasChanged = true;
        }

        return hasChanged
            ? TaskMutationExecutionResult.ChangedResult()
            : TaskMutationExecutionResult.NoChanges();
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

    public TaskMutationExecutionResult ReplaceSubtasks(TaskMutationAuthorization authorization, IEnumerable<TaskSubtask> updatedSubtasks, WorkflowState? reviewState, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.ReplaceSubtasks, out var lockedResult))
        {
            return lockedResult;
        }

        var normalizedSubtasks = updatedSubtasks
            .OrderBy(subtask => subtask.Order)
            .Select((subtask, index) => new TaskSubtask(subtask.Id, subtask.Title, subtask.IsChecked, index))
            .ToArray();

        var hasChanged = subtasks.Count != normalizedSubtasks.Length
            || subtasks.Zip(normalizedSubtasks, (current, updated) => current != updated).Any(changed => changed);

        subtasks.Clear();
        subtasks.AddRange(normalizedSubtasks);

        if (hasChanged)
        {
            activityTimeline.Add(ActivityTimelineItem.TaskChecklistChanged(changedAt));
        }

        if (LifecycleState == TaskLifecycleState.ActiveRecord
            && CurrentState.IsCompletedState is false
            && reviewState is not null
            && subtasks.Count > 0
            && subtasks.All(subtask => subtask.IsChecked)
            && CurrentState.Key != reviewState.Key)
        {
            ChangeState(TaskMutationAuthorization.Granted(TaskMutationKind.ChangeWorkflowState), reviewState, changedAt);
            return TaskMutationExecutionResult.ChangedResult();
        }

        return hasChanged
            ? TaskMutationExecutionResult.ChangedResult()
            : TaskMutationExecutionResult.NoChanges();
    }

    /// <summary>
    /// 驗證 mutation authorization 是否與預期操作一致，並在鎖定時回傳 locked result。
    /// </summary>
    private static bool TryRejectLockedMutation(TaskMutationAuthorization authorization, TaskMutationKind expectedKind, out TaskMutationExecutionResult lockedResult)
    {
        if (authorization.Kind != expectedKind)
        {
            throw new ArgumentException($"Mutation authorization kind mismatch. Expected {expectedKind} but received {authorization.Kind}.", nameof(authorization));
        }

        lockedResult = TaskMutationExecutionResult.LockedResult();
        return authorization.IsLocked;
    }

    /// <summary>
    /// 將任務聚合轉成對外輸出的 task model。
    /// </summary>
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
            subtasks.OrderBy(subtask => subtask.Order).Select(subtask => subtask.ToModel()).ToArray(),
            reminders.Select(reminder => reminder.ToModel()).ToArray(),
            codeTraceability.ToModel(),
            activityTimeline.Select(item => item.ToModel()).ToArray());
    }
}