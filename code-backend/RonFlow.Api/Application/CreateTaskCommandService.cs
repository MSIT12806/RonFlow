using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class CreateTaskCommandService(IProjectRepository projectRepository, TimeProvider timeProvider)
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

        var task = project.CreateTask(taskTitle!, timeProvider.GetUtcNow());
        projectRepository.Update(project);

        return CreateTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
