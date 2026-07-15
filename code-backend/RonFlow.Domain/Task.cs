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
        Guid? parentTaskId,
        string title,
        string description,
        WorkflowState currentState,
        bool isInFlow,
        bool isSplitComplete,
        bool isShort,
        TaskLifecycleState lifecycleState,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset mutationAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? archivedAt,
        DateTimeOffset? trashedAt,
        int sortOrder,
        TaskEstimatedEffort? estimatedEffort,
        TaskEstimatedEffort? completedEffort,
        IEnumerable<TaskSubtask> subtasks,
        IEnumerable<TaskReminder> reminders,
        TaskCodeTraceability codeTraceability,
        IEnumerable<ActivityTimelineItem> activityTimeline)
    {
        Id = id;
        ProjectId = projectId;
        ParentTaskId = parentTaskId;
        Title = title;
        Description = description;
        CurrentState = currentState;
        IsInFlow = isInFlow;
        IsSplitComplete = isSplitComplete;
        IsShort = isShort;
        LifecycleState = lifecycleState;
        DueDate = dueDate;
        CreatedAt = createdAt;
        MutationAt = mutationAt;
        CompletedAt = completedAt;
        ArchivedAt = archivedAt;
        TrashedAt = trashedAt;
        SortOrder = sortOrder;
        EstimatedEffort = estimatedEffort;
        CompletedEffort = completedEffort;
        this.subtasks = subtasks.OrderBy(subtask => subtask.Order).ToList();
        this.reminders = reminders.ToList();
        this.codeTraceability = codeTraceability;
        this.activityTimeline = activityTimeline.ToList();
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public Guid? ParentTaskId { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public WorkflowState CurrentState { get; private set; }

    public bool IsInFlow { get; private set; }

    public bool IsSplitComplete { get; private set; }

    public bool IsShort { get; private set; }

    public TaskLifecycleState LifecycleState { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset MutationAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public DateTimeOffset? TrashedAt { get; private set; }

    public int SortOrder { get; private set; }

    public TaskEstimatedEffort? EstimatedEffort { get; private set; }

    public TaskEstimatedEffort? CompletedEffort { get; private set; }

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
        bool isInFlow = false,
        Guid? parentTaskId = null,
        IEnumerable<TaskSubtask>? subtasks = null,
        bool isShort = false)
    {
        TaskEstimatedEffort? estimatedEffort = null;
        if (isShort)
        {
            TaskEstimatedEffort.TryCreate(15, "minutes", out estimatedEffort);
        }

        return new Task(
            Guid.NewGuid(),
            projectId,
            parentTaskId,
            title.Value,
            string.Empty,
            initialState,
            isInFlow || isShort,
            false,
            isShort,
            TaskLifecycleState.ActiveRecord,
            null,
            createdAt,
            createdAt,
            null,
            null,
            null,
            sortOrder,
            estimatedEffort,
            null,
            subtasks ?? [],
            [],
            TaskCodeTraceability.Empty,
            [ActivityTimelineItem.TaskCreated(createdAt)]);
    }

    public static Task Duplicate(
        Task source,
        TaskTitle title,
        WorkflowState initialState,
        DateTimeOffset createdAt,
        int sortOrder,
        Guid? parentTaskId)
    {
        return new Task(
            Guid.NewGuid(),
            source.ProjectId,
            parentTaskId,
            title.Value,
            source.Description,
            initialState,
            false,
            false,
            false,
            TaskLifecycleState.ActiveRecord,
            source.DueDate,
            createdAt,
            createdAt,
            null,
            null,
            null,
            sortOrder,
            source.EstimatedEffort,
            null,
            source.Subtasks.Select(subtask => new TaskSubtask(Guid.NewGuid(), subtask.Title, subtask.IsChecked, subtask.Order)),
            [],
            CloneCodeTraceability(source.CodeTraceability),
            [ActivityTimelineItem.TaskCreated(createdAt)]);
    }

    public static Task Rehydrate(
        Guid id,
        Guid projectId,
        Guid? parentTaskId,
        string title,
        string description,
        WorkflowState currentState,
        bool isInFlow,
        bool isSplitComplete,
        bool isShort,
        TaskLifecycleState lifecycleState,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset mutationAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? archivedAt,
        DateTimeOffset? trashedAt,
        int sortOrder,
        TaskEstimatedEffort? estimatedEffort,
        IEnumerable<TaskSubtask> subtasks,
        IEnumerable<TaskReminder> reminders,
        TaskCodeTraceability codeTraceability,
        IEnumerable<ActivityTimelineItem> activityTimeline,
        TaskEstimatedEffort? completedEffort = null)
    {
        return new Task(id, projectId, parentTaskId, title, description, currentState, isInFlow, isSplitComplete, isShort, lifecycleState, dueDate, createdAt, mutationAt, completedAt, archivedAt, trashedAt, sortOrder, estimatedEffort, completedEffort, subtasks, reminders, codeTraceability, activityTimeline);
    }

    public TaskMutationExecutionResult MarkChildTaskAdded(TaskMutationAuthorization authorization, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.CreateChildTask, out var lockedResult))
        {
            return lockedResult;
        }

        if (IsInFlow)
        {
            IsInFlow = false;
        }

        activityTimeline.Add(ActivityTimelineItem.ChildTaskAdded(changedAt));
        MutationAt = changedAt;
        return TaskMutationExecutionResult.ChangedResult();
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
        MutationAt = changedAt;
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
        MutationAt = changedAt;
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
            MutationAt = dispatchedAt;
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
        MutationAt = changedAt;
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
        MutationAt = changedAt;
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
        MutationAt = changedAt;
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
        MutationAt = changedAt;
        return TaskMutationExecutionResult.ChangedResult();
    }

    public TaskMutationExecutionResult ChangeState(TaskMutationAuthorization authorization, WorkflowState targetState, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.ChangeWorkflowState, out var lockedResult))
        {
            return lockedResult;
        }

        var wasInFlow = IsInFlow;
        IsInFlow = true;

        if (CurrentState.Key == targetState.Key)
        {
            if (!wasInFlow)
            {
                MutationAt = changedAt;
            }

            return wasInFlow
                ? TaskMutationExecutionResult.NoChanges()
                : TaskMutationExecutionResult.ChangedResult();
        }

        var wasDone = CurrentState.IsCompletedState;
        var isDone = targetState.IsCompletedState;

        CurrentState = targetState;
        activityTimeline.Add(ActivityTimelineItem.TaskStateChanged(targetState.Label, changedAt));

        if (!wasDone && isDone)
        {
            CompletedAt = changedAt;
            CompletedEffort = EstimatedEffort;
            activityTimeline.Add(ActivityTimelineItem.TaskCompleted(changedAt));
        }

        if (wasDone && !isDone)
        {
            CompletedAt = null;
            CompletedEffort = null;
            activityTimeline.Add(ActivityTimelineItem.TaskReopened(changedAt));
        }

        MutationAt = changedAt;
        return TaskMutationExecutionResult.ChangedResult();
    }

    public bool CompleteFromChildren(WorkflowState completedState, DateTimeOffset changedAt)
    {
        if (LifecycleState != TaskLifecycleState.ActiveRecord || CurrentState.IsCompletedState)
        {
            return false;
        }

        CurrentState = completedState;
        CompletedAt = changedAt;
        CompletedEffort = EstimatedEffort;
        activityTimeline.Add(ActivityTimelineItem.TaskStateChanged(completedState.Label, changedAt));
        activityTimeline.Add(ActivityTimelineItem.TaskCompleted(changedAt));
        MutationAt = changedAt;
        return true;
    }

    public TaskMutationExecutionResult UpdateDetails(TaskMutationAuthorization authorization, TaskTitle title, string description, DateOnly? dueDate, TaskEstimatedEffort? estimatedEffort, TaskCodeTraceability? nextCodeTraceability, DateTimeOffset changedAt, bool? isShort = null)
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

        if (EstimatedEffort != estimatedEffort)
        {
            EstimatedEffort = estimatedEffort;
            activityTimeline.Add(ActivityTimelineItem.TaskEstimatedEffortChanged(changedAt));
            hasChanged = true;
        }

        if (isShort is not null && IsShort != isShort.Value)
        {
            IsShort = isShort.Value;
            hasChanged = true;
        }

        if (nextCodeTraceability is not null && codeTraceability != nextCodeTraceability)
        {
            codeTraceability = nextCodeTraceability;
            activityTimeline.Add(ActivityTimelineItem.TaskCodeTraceabilityChanged(changedAt));
            hasChanged = true;
        }

        return hasChanged
            ? ChangedAt(changedAt)
            : TaskMutationExecutionResult.NoChanges();
    }

    public TaskMutationExecutionResult SetSplitComplete(TaskMutationAuthorization authorization, bool isSplitComplete, DateTimeOffset changedAt)
    {
        if (TryRejectLockedMutation(authorization, TaskMutationKind.SetSplitComplete, out var lockedResult))
        {
            return lockedResult;
        }

        if (IsSplitComplete == isSplitComplete)
        {
            return TaskMutationExecutionResult.NoChanges();
        }

        IsSplitComplete = isSplitComplete;
        activityTimeline.Add(isSplitComplete
            ? ActivityTimelineItem.TaskSplitCompleted(changedAt)
            : ActivityTimelineItem.TaskSplitCompletionCleared(changedAt));
        MutationAt = changedAt;
        return TaskMutationExecutionResult.ChangedResult();
    }

    public bool UpdateSortOrder(int sortOrder, DateTimeOffset changedAt, bool recordActivity)
    {
        var hasChanged = SortOrder != sortOrder;
        SortOrder = sortOrder;

        if (recordActivity)
        {
            activityTimeline.Add(ActivityTimelineItem.TaskReordered(changedAt));
        }

        if (hasChanged || recordActivity)
        {
            MutationAt = changedAt;
        }

        return true;
    }

    public bool UpdateTreePosition(Guid? parentTaskId, DateTimeOffset changedAt, bool recordActivity)
    {
        var hasChanged = ParentTaskId != parentTaskId;
        ParentTaskId = parentTaskId;

        if (recordActivity)
        {
            activityTimeline.Add(ActivityTimelineItem.TaskReordered(changedAt));
        }

        if (hasChanged || recordActivity)
        {
            MutationAt = changedAt;
        }

        return hasChanged;
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
            MutationAt = changedAt;
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

    private TaskMutationExecutionResult ChangedAt(DateTimeOffset changedAt)
    {
        MutationAt = changedAt;
        return TaskMutationExecutionResult.ChangedResult();
    }

    private static TaskCodeTraceability CloneCodeTraceability(TaskCodeTraceability source)
    {
        return new TaskCodeTraceability(
            source.Api.Select(item => new TaskCodeTraceabilityItem(item.ChangeType, item.Target)).ToArray(),
            source.FrontendPages.Select(item => new TaskCodeTraceabilityItem(item.ChangeType, item.Target)).ToArray(),
            source.FrontendComponents.Select(item => new TaskCodeTraceabilityItem(item.ChangeType, item.Target)).ToArray());
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
            ParentTaskId,
            Title,
            Description,
            CurrentState.ToModel(),
            IsInFlow,
            IsSplitComplete,
            IsShort,
            LifecycleState,
            DueDate,
            CreatedAt,
            MutationAt,
            CompletedAt,
            ArchivedAt,
            TrashedAt,
            SortOrder,
            EstimatedEffort?.ToModel(),
            subtasks.OrderBy(subtask => subtask.Order).Select(subtask => subtask.ToModel()).ToArray(),
            reminders.Select(reminder => reminder.ToModel()).ToArray(),
            codeTraceability.ToModel(),
            activityTimeline.Select(item => item.ToModel()).ToArray(),
            CompletedEffort?.ToModel());
    }
}
