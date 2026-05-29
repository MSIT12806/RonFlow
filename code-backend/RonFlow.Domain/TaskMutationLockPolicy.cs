namespace RonFlow.Domain;

public enum TaskMutationKind
{
    UpdateDetails,
    ReplaceSubtasks,
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

public static class TaskMutationLockPolicy
{
    public static TaskMutationLockRequirement Resolve(TaskMutationKind mutationKind)
    {
        return mutationKind switch
        {
            TaskMutationKind.UpdateDetails => TaskMutationLockRequirement.RequireOwnedLock,
            TaskMutationKind.ReplaceSubtasks => TaskMutationLockRequirement.RequireUnlockedOrOwnedLock,
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