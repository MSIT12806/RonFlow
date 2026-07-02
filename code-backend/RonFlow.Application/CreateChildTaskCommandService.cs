using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Application;

public sealed class CreateChildTaskCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskMutationGuard taskMutationGuard,
    TimeProvider timeProvider)
{
    public CreateChildTaskResult Create(Guid currentUserId, Guid projectId, Guid parentTaskId, string? rawTitle)
    {
        if (!TaskTitle.TryCreate(rawTitle, out var taskTitle))
        {
            return CreateChildTaskResult.Invalid("title", "任務標題為必填欄位");
        }

        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return CreateChildTaskResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return CreateChildTaskResult.Denied();
        }

        var parentTask = taskRepository.Get(parentTaskId);
        if (parentTask is null || parentTask.ProjectId != projectId || parentTask.LifecycleState != TaskLifecycleState.ActiveRecord)
        {
            return CreateChildTaskResult.NotFound();
        }

        var changedAt = timeProvider.GetUtcNow();
        var parentMutationResult = parentTask.MarkChildTaskAdded(
            taskMutationGuard.Authorize(currentUserId, parentTaskId, TaskMutationKind.CreateChildTask),
            changedAt);

        if (parentMutationResult.Locked)
        {
            return CreateChildTaskResult.Locked();
        }

        var project = access.Project!;
        var sortOrder = taskRepository.GetByProjectId(project.Id).Count;
        var childTask = DomainTask.Create(
            project.Id,
            taskTitle!,
            project.GetDefaultWorkflowState(),
            changedAt,
            sortOrder,
            parentTaskId: parentTaskId,
            subtasks: project.CreateSubtasksFromTemplates());

        taskRepository.Add(childTask);
        taskRepository.Update(parentTask);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return CreateChildTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(childTask.ToModel()));
    }
}
