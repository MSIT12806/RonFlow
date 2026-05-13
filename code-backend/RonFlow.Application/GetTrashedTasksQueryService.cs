using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetTrashedTasksQueryService(IProjectRepository projectRepository, ITaskRepository taskRepository)
{
    public LifecycleTaskListView? Get(Guid projectId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return null;
        }

        var tasks = taskRepository.GetByProjectId(projectId)
            .Select(task => task.ToModel())
            .ToArray();

        return CoreFlowReadModelFactory.CreateLifecycleTaskList(project, tasks, TaskLifecycleState.Trashed);
    }
}