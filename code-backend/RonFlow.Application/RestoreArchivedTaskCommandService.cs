using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class RestoreArchivedTaskCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskMutationGuard taskMutationGuard,
    TimeProvider timeProvider)
{
    public TaskLifecycleCommandResult Restore(Guid currentUserId, Guid projectId, Guid taskId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return TaskLifecycleCommandResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return TaskLifecycleCommandResult.Denied();
        }

        var project = access.Project!;

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return TaskLifecycleCommandResult.NotFound();
        }

        var changedAt = timeProvider.GetUtcNow();
        var nextSortOrder = taskRepository.GetByProjectId(projectId)
            .Where(projectTask => projectTask.LifecycleState == TaskLifecycleState.ActiveRecord)
            .Where(projectTask => projectTask.CurrentState.Key == task.CurrentState.Key)
            .Select(projectTask => projectTask.SortOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        var mutationResult = task.RestoreFromArchive(
            taskMutationGuard.Authorize(currentUserId, taskId, TaskMutationKind.RestoreFromArchive),
            nextSortOrder,
            changedAt);

        if (mutationResult.Locked)
        {
            return TaskLifecycleCommandResult.Locked();
        }

        taskRepository.Update(task);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return TaskLifecycleCommandResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}