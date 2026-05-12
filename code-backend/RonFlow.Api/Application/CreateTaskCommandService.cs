using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Application;

public sealed class CreateTaskCommandService(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public CreateTaskResult Create(Guid projectId, string? rawTitle)
    {
        if (!TaskTitle.TryCreate(rawTitle, out var taskTitle))
        {
            return CreateTaskResult.Invalid("title", "任務標題為必填欄位");
        }

        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return CreateTaskResult.NotFound();
        }

        var createdAt = timeProvider.GetUtcNow();
        var task = DomainTask.Create(project.Id, taskTitle!, project.GetDefaultWorkflowState(), createdAt);
        taskRepository.Add(task);

        project.Touch(createdAt);
        projectRepository.Update(project);

        return CreateTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
