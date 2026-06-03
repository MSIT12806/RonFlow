using RonFlow.Domain;

namespace RonFlow.Application;

/// <summary>
/// 協調建立任務提醒流程。
/// </summary>
public sealed class CreateTaskReminderCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskMutationGuard taskMutationGuard,
    TimeProvider timeProvider)
{
    /// <summary>
    /// 在指定任務上建立一筆提醒並回傳最新任務資料。
    /// </summary>
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

        var changedAt = GetReminderChangedAt();
        var mutationResult = task.AddReminder(
            taskMutationGuard.Authorize(currentUserId, taskId, TaskMutationKind.CreateReminder),
            rawReminderDateTime,
            rawDescription?.Trim() ?? string.Empty,
            changedAt);

        if (mutationResult.Locked)
        {
            return CreateTaskReminderResult.Locked();
        }

        taskRepository.Update(task);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return CreateTaskReminderResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }

    /// <summary>
    /// 取得這次建立任務提醒流程要使用的異動時間。
    /// </summary>
    private DateTimeOffset GetReminderChangedAt()
    {
        return timeProvider.GetUtcNow();
    }
}