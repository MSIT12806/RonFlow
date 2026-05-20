using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class DeleteTaskReminderCommandService(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public DeleteTaskReminderResult Delete(Guid projectId, Guid taskId, Guid reminderId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return DeleteTaskReminderResult.TaskMissing();
        }

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return DeleteTaskReminderResult.TaskMissing();
        }

        var changedAt = timeProvider.GetUtcNow();
        var deleted = task.DeleteReminder(reminderId, changedAt);
        if (!deleted)
        {
            return DeleteTaskReminderResult.ReminderMissing();
        }

        taskRepository.Update(task);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return DeleteTaskReminderResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}