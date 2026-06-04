using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Application;

public sealed class CreateTaskCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    IWorkflowThroughputProjectionOutbox workflowThroughputProjectionOutbox,
    TimeProvider timeProvider)
{
    public CreateTaskCommandService(
        IProjectRepository projectRepository,
        ITaskRepository taskRepository,
        TimeProvider timeProvider)
        : this(projectRepository, new ProjectAccessService(projectRepository), taskRepository, new NoOpWorkflowThroughputProjectionOutbox(), timeProvider)
    {
    }

    public CreateTaskResult Create(Guid projectId, string? rawTitle)
    {
        return Create(Guid.Empty, projectId, rawTitle);
    }

    public CreateTaskResult Create(Guid currentUserId, Guid projectId, string? rawTitle)
    {
        if (!TaskTitle.TryCreate(rawTitle, out var taskTitle))
        {
            return CreateTaskResult.Invalid("title", "任務標題為必填欄位");
        }

        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return CreateTaskResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return CreateTaskResult.Denied();
        }

        var project = access.Project!;

        var createdAt = timeProvider.GetUtcNow();
        var sortOrder = taskRepository.GetByProjectId(project.Id).Count;
        var task = DomainTask.Create(
            project.Id,
            taskTitle!,
            project.GetDefaultWorkflowState(),
            createdAt,
            sortOrder,
            project.CreateSubtasksFromTemplates());
        taskRepository.Add(task);
        workflowThroughputProjectionOutbox.EnqueueTaskCreated(project.Id, task.Id, createdAt);

        project.Touch(createdAt);
        projectRepository.Update(project);

        return CreateTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
