namespace RonFlow.Domain;

/// <summary>
/// 定義任務異動的種類。
/// </summary>
public enum TaskMutationKind
{
    UpdateDetails,
    SetSplitComplete,
    ReplaceSubtasks,
    CreateChildTask,
    CreateReminder,
    DeleteReminder,
    Archive,
    MoveToTrash,
    RestoreFromArchive,
    RestoreFromTrash,
    ChangeWorkflowState,
}

public enum TaskMutationLockRequirement
{
    None,
    RequireOwnedLock,
    RequireUnlocked,
    RequireUnlockedOrOwnedLock,
}

/// <summary>
/// 將任務異動種類對應到需要的鎖定規則。
/// </summary>
public static class TaskMutationLockPolicy
{
    /// <summary>
    /// 解析指定任務異動所需的鎖定條件。
    /// </summary>
    public static TaskMutationLockRequirement Resolve(TaskMutationKind mutationKind)
    {
        return mutationKind switch
        {
            TaskMutationKind.UpdateDetails => TaskMutationLockRequirement.RequireOwnedLock,
            TaskMutationKind.SetSplitComplete => TaskMutationLockRequirement.RequireUnlockedOrOwnedLock,
            TaskMutationKind.ReplaceSubtasks => TaskMutationLockRequirement.RequireUnlockedOrOwnedLock,
            TaskMutationKind.CreateChildTask => TaskMutationLockRequirement.RequireUnlockedOrOwnedLock,
            TaskMutationKind.CreateReminder => TaskMutationLockRequirement.RequireOwnedLock,
            TaskMutationKind.DeleteReminder => TaskMutationLockRequirement.RequireOwnedLock,
            TaskMutationKind.Archive => TaskMutationLockRequirement.RequireUnlocked,
            TaskMutationKind.MoveToTrash => TaskMutationLockRequirement.RequireUnlocked,
            TaskMutationKind.RestoreFromArchive => TaskMutationLockRequirement.None,
            TaskMutationKind.RestoreFromTrash => TaskMutationLockRequirement.None,
            TaskMutationKind.ChangeWorkflowState => TaskMutationLockRequirement.None,
            _ => throw new ArgumentOutOfRangeException(nameof(mutationKind), mutationKind, null),
        };
    }
}
