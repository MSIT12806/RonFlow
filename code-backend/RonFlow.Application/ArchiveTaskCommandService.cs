using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ArchiveTaskCommandService(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public TaskLifecycleCommandResult Archive(Guid projectId, Guid taskId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return TaskLifecycleCommandResult.NotFound();
        }

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return TaskLifecycleCommandResult.NotFound();
        }

        var changedAt = timeProvider.GetUtcNow();
        task.Archive(changedAt);
        taskRepository.Update(task);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return TaskLifecycleCommandResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}