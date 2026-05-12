using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class UpdateTaskCommandService(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public UpdateTaskResult Update(Guid projectId, Guid taskId, string? rawTitle, string? rawDescription, DateOnly? dueDate)
    {
        if (!TaskTitle.TryCreate(rawTitle, out var taskTitle))
        {
            return UpdateTaskResult.Invalid("title", "任務標題為必填欄位");
        }

        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return UpdateTaskResult.NotFound();
        }

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return UpdateTaskResult.NotFound();
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