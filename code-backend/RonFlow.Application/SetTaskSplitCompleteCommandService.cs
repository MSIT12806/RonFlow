using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class SetTaskSplitCompleteCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskMutationGuard taskMutationGuard,
    TimeProvider timeProvider)
{
    public SetTaskSplitCompleteResult Set(
        Guid currentUserId,
        Guid projectId,
        Guid taskId,
        bool isSplitComplete)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return SetTaskSplitCompleteResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return SetTaskSplitCompleteResult.Denied();
        }

        var project = access.Project!;
        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return SetTaskSplitCompleteResult.NotFound();
        }

        var hasActiveChildTasks = taskRepository.GetByProjectId(projectId)
            .Any(projectTask => projectTask.LifecycleState == TaskLifecycleState.ActiveRecord && projectTask.ParentTaskId == taskId);

        if (!hasActiveChildTasks && !task.IsSplitComplete)
        {
            return SetTaskSplitCompleteResult.Invalid("isSplitComplete", "只有父任務可以標記拆解完成");
        }

        if (isSplitComplete && !hasActiveChildTasks)
        {
            return SetTaskSplitCompleteResult.Invalid("isSplitComplete", "只有父任務可以標記拆解完成");
        }

        var changedAt = timeProvider.GetUtcNow();
        var mutationResult = task.SetSplitComplete(
            taskMutationGuard.Authorize(currentUserId, taskId, TaskMutationKind.SetSplitComplete),
            isSplitComplete,
            changedAt);

        if (mutationResult.Locked)
        {
            return SetTaskSplitCompleteResult.Locked();
        }

        taskRepository.Update(task);

        if (mutationResult.Changed)
        {
            project.Touch(changedAt);
            projectRepository.Update(project);
        }

        return SetTaskSplitCompleteResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
