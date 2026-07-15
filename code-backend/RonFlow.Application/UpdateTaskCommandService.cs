using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class UpdateTaskCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskMutationGuard taskMutationGuard,
    TimeProvider timeProvider)
{
    public UpdateTaskResult Update(
        Guid currentUserId,
        Guid projectId,
        Guid taskId,
        string? rawTitle,
        string? rawDescription,
        DateOnly? dueDate,
        TaskEstimatedEffort? estimatedEffort,
        TaskCodeTraceability? codeTraceability,
        bool? isShort = null)
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

        if (isShort == true)
        {
            if (project.SubtaskTemplates.Count == 0)
            {
                return UpdateTaskResult.Invalid("isShort", "專案尚未設定完成條件模板，無法使用 short 任務");
            }

            var hasActiveChildren = taskRepository.GetByProjectId(projectId)
                .Any(projectTask => projectTask.ParentTaskId == task.Id && projectTask.LifecycleState == TaskLifecycleState.ActiveRecord);
            if (hasActiveChildren)
            {
                return UpdateTaskResult.Invalid("isShort", "父任務不可使用 short 任務");
            }

            if (task.Subtasks.Count == 0)
            {
                return UpdateTaskResult.Invalid("isShort", "short 任務至少需要一筆完成條件才能送進 Flow");
            }

            TaskEstimatedEffort.TryCreate(15, "minutes", out estimatedEffort);
        }

        var changedAt = timeProvider.GetUtcNow();
        var mutationResult = task.UpdateDetails(
            taskMutationGuard.Authorize(currentUserId, taskId, TaskMutationKind.UpdateDetails),
            taskTitle!,
            rawDescription?.Trim() ?? string.Empty,
            dueDate,
            estimatedEffort,
            codeTraceability,
            changedAt,
            isShort);

        if (mutationResult.Locked)
        {
            return UpdateTaskResult.Locked();
        }

        if (isShort == true && !task.IsInFlow)
        {
            var flowMutation = task.ChangeState(
                taskMutationGuard.Authorize(currentUserId, taskId, TaskMutationKind.ChangeWorkflowState),
                project.GetDefaultWorkflowState(),
                changedAt);
            if (flowMutation.Locked)
            {
                return UpdateTaskResult.Locked();
            }
        }

        taskRepository.Update(task);

        if (mutationResult.Changed)
        {
            project.Touch(changedAt);
            projectRepository.Update(project);
        }

        return UpdateTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
