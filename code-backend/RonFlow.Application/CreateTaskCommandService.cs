using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Application;

public sealed class CreateTaskCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public CreateTaskCommandService(
        IProjectRepository projectRepository,
        ITaskRepository taskRepository,
        TimeProvider timeProvider)
        : this(projectRepository, new ProjectAccessService(projectRepository), taskRepository, timeProvider)
    {
    }

    public CreateTaskResult Create(Guid projectId, string? rawTitle, bool isShort = false)
    {
        return Create(Guid.Empty, projectId, rawTitle, isShort);
    }

    public CreateTaskResult Create(Guid currentUserId, Guid projectId, string? rawTitle, bool isShort = false)
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

        if (isShort && project.SubtaskTemplates.Count == 0)
        {
            return CreateTaskResult.Invalid("isShort", "專案尚未設定完成條件模板，無法建立 short 任務");
        }

        var createdAt = timeProvider.GetUtcNow();
        var sortOrder = taskRepository.GetByProjectId(project.Id).Count;
        var task = DomainTask.Create(
            project.Id,
            taskTitle!,
            project.GetDefaultWorkflowState(),
            createdAt,
            sortOrder,
            subtasks: project.CreateSubtasksFromTemplates(),
            isShort: isShort);
        taskRepository.Add(task);

        project.Touch(createdAt);
        projectRepository.Update(project);

        return CreateTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
