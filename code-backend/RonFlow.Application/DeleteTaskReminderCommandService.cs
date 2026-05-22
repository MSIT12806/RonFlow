using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class DeleteTaskReminderCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public DeleteTaskReminderResult Delete(Guid currentUserId, Guid projectId, Guid taskId, Guid reminderId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return DeleteTaskReminderResult.TaskMissing();
        }

        if (access.AccessDenied)
        {
            return DeleteTaskReminderResult.Denied();
        }

        var project = access.Project!;

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