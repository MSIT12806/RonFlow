using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ChangeTaskStateCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskMutationGuard taskMutationGuard,
    TimeProvider timeProvider)
{
    public ChangeTaskStateCommandService(
        IProjectRepository projectRepository,
        ITaskRepository taskRepository,
        TimeProvider timeProvider)
        : this(projectRepository, new ProjectAccessService(projectRepository), taskRepository, new TaskMutationGuard(new TaskContentEditLockService()), timeProvider)
    {
    }

    public ChangeTaskStateResult Change(Guid projectId, Guid taskId, string stateKey)
    {
        return Change(Guid.Empty, projectId, taskId, stateKey);
    }

    public ChangeTaskStateResult Change(Guid currentUserId, Guid projectId, Guid taskId, string stateKey)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return ChangeTaskStateResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return ChangeTaskStateResult.Denied();
        }

        var project = access.Project!;

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return ChangeTaskStateResult.NotFound();
        }

        var targetState = project.FindWorkflowState(stateKey);
        if (targetState is null)
        {
            return ChangeTaskStateResult.Invalid("stateKey", "指定的狀態不存在於此專案 workflow");
        }

        var changedAt = timeProvider.GetUtcNow();
        var mutationResult = task.ChangeState(
            taskMutationGuard.Authorize(currentUserId, taskId, TaskMutationKind.ChangeWorkflowState),
            targetState,
            changedAt);

        if (mutationResult.Locked)
        {
            return ChangeTaskStateResult.Locked();
        }

        taskRepository.Update(task);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return ChangeTaskStateResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
