using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class UpdateTaskCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskContentEditLockService taskContentEditLockService,
    TimeProvider timeProvider)
{
    public UpdateTaskResult Update(Guid currentUserId, Guid projectId, Guid taskId, string? rawTitle, string? rawDescription, DateOnly? dueDate)
    {
        if (!TaskTitle.TryCreate(rawTitle, out var taskTitle))
        {
            return UpdateTaskResult.Invalid("title", "任務標題為必填欄位");
        }

        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return UpdateTaskResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return UpdateTaskResult.Denied();
        }

        var project = access.Project!;

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return UpdateTaskResult.NotFound();
        }

        if (!taskContentEditLockService.IsHeldBy(currentUserId, taskId))
        {
            return UpdateTaskResult.Locked();
        }

        var changedAt = timeProvider.GetUtcNow();
        var hasChanged = task.UpdateDetails(taskTitle!, rawDescription?.Trim() ?? string.Empty, dueDate, changedAt);
        taskRepository.Update(task);

        if (hasChanged)
        {
            project.Touch(changedAt);
            projectRepository.Update(project);
        }

        return UpdateTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}