namespace RonFlow.Domain;

public enum TaskMutationAuthorizationStatus
{
    Granted,
    Locked,
}

public readonly record struct TaskMutationAuthorization(TaskMutationKind Kind, TaskMutationAuthorizationStatus Status)
{
    public bool IsGranted => Status == TaskMutationAuthorizationStatus.Granted;

    public bool IsLocked => Status == TaskMutationAuthorizationStatus.Locked;

    public static TaskMutationAuthorization Granted(TaskMutationKind kind)
    {
        return new(kind, TaskMutationAuthorizationStatus.Granted);
    }

    public static TaskMutationAuthorization Locked(TaskMutationKind kind)
    {
        return new(kind, TaskMutationAuthorizationStatus.Locked);
    }
}

public sealed record TaskMutationExecutionResult(bool Succeeded, bool Changed, bool Locked)
{
    public static TaskMutationExecutionResult ChangedResult()
    {
        return new(true, true, false);
    }

    public static TaskMutationExecutionResult NoChanges()
    {
        return new(true, false, false);
    }

    public static TaskMutationExecutionResult LockedResult()
    {
        return new(false, false, true);
    }
}

public sealed record TaskDeleteReminderExecutionResult(bool Succeeded, bool Changed, bool Locked, bool ReminderNotFound)
{
    public static TaskDeleteReminderExecutionResult ChangedResult()
    {
        return new(true, true, false, false);
    }

    public static TaskDeleteReminderExecutionResult ReminderMissing()
    {
        return new(false, false, false, true);
    }

    public static TaskDeleteReminderExecutionResult LockedResult()
    {
        return new(false, false, true, false);
    }
}