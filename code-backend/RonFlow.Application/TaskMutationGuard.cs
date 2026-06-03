using RonFlow.Domain;

namespace RonFlow.Application;

/// <summary>
/// 依任務異動種類判斷目前使用者是否可執行異動。
/// </summary>
public sealed class TaskMutationGuard(TaskContentEditLockService taskContentEditLockService)
{
    /// <summary>
    /// 根據 mutation kind 與目前鎖定狀態產生授權結果。
    /// </summary>
    public TaskMutationAuthorization Authorize(Guid currentUserId, Guid taskId, TaskMutationKind mutationKind)
    {
        var requirement = TaskMutationLockPolicy.Resolve(mutationKind);

        var isGranted = requirement switch
        {
            TaskMutationLockRequirement.None => true,
            TaskMutationLockRequirement.RequireOwnedLock => taskContentEditLockService.IsHeldBy(currentUserId, taskId),
            TaskMutationLockRequirement.RequireUnlocked => !taskContentEditLockService.IsLocked(taskId),
            TaskMutationLockRequirement.RequireUnlockedOrOwnedLock => !taskContentEditLockService.IsHeldByAnotherUser(currentUserId, taskId),
            _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null),
        };

        return isGranted
            ? TaskMutationAuthorization.Granted(mutationKind)
            : TaskMutationAuthorization.Locked(mutationKind);
    }
}