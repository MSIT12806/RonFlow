using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class InMemoryCoreFlowReadStore(IProjectRepository projectRepository, ITaskRepository taskRepository) : ICoreFlowReadStore
{
    public IReadOnlyList<ProjectSummaryModel> GetProjects()
    {
        return projectRepository.GetProjects();
    }

    public ProjectBoardModel? GetProjectBoard(Guid projectId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return null;
        }

        return new ProjectBoardModel(
            project.Id,
            project.Name,
            project.WorkflowStates.Select(state => state.ToModel()).ToArray(),
            taskRepository.GetByProjectId(projectId)
                .Select(task => task.ToModel())
                .ToArray());
    }

    public TaskModel? GetTaskDetail(Guid projectId, Guid taskId)
    {
        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return null;
        }

        return task.ToModel();
    }
}