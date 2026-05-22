using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class CreateTaskReminderCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public CreateTaskReminderResult Create(Guid currentUserId, Guid projectId, Guid taskId, string? rawReminderDateTime, string? rawDescription)
    {
        if (string.IsNullOrWhiteSpace(rawReminderDateTime))
        {
            return CreateTaskReminderResult.Invalid("reminderDateTime", "提醒時間為必填欄位");
        }

        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return CreateTaskReminderResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return CreateTaskReminderResult.Denied();
        }

        var project = access.Project!;

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return CreateTaskReminderResult.NotFound();
        }

        var changedAt = timeProvider.GetUtcNow();
        task.AddReminder(rawReminderDateTime, rawDescription?.Trim() ?? string.Empty, changedAt);
        taskRepository.Update(task);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return CreateTaskReminderResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}