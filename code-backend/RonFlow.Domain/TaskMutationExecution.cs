namespace RonFlow.Domain;

public enum TaskMutationAuthorizationStatus
{
    Granted,
    Locked,
}

/// <summary>
/// 表示某個任務異動是否獲准執行。
/// </summary>
public readonly record struct TaskMutationAuthorization(TaskMutationKind Kind, TaskMutationAuthorizationStatus Status)
{
    public bool IsGranted => Status == TaskMutationAuthorizationStatus.Granted;

    public bool IsLocked => Status == TaskMutationAuthorizationStatus.Locked;

    /// <summary>
    /// 建立已獲准的任務異動授權。
    /// </summary>
    public static TaskMutationAuthorization Granted(TaskMutationKind kind)
    {
        return new(kind, TaskMutationAuthorizationStatus.Granted);
    }

    /// <summary>
    /// 建立因鎖定而被拒絕的任務異動授權。
    /// </summary>
    public static TaskMutationAuthorization Locked(TaskMutationKind kind)
    {
        return new(kind, TaskMutationAuthorizationStatus.Locked);
    }
}

/// <summary>
/// 表示任務異動執行結果。
/// </summary>
public sealed record TaskMutationExecutionResult(bool Succeeded, bool Changed, bool Locked)
{
    /// <summary>
    /// 回傳代表資料已成功異動的結果。
    /// </summary>
    public static TaskMutationExecutionResult ChangedResult()
    {
        return new(true, true, false);
    }

    /// <summary>
    /// 回傳代表請求有效但沒有實際異動的結果。
    /// </summary>
    public static TaskMutationExecutionResult NoChanges()
    {
        return new(true, false, false);
    }

    /// <summary>
    /// 回傳代表異動因編輯鎖而被阻擋的結果。
    /// </summary>
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