using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class TaskMutationGuard(TaskContentEditLockService taskContentEditLockService)
{
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