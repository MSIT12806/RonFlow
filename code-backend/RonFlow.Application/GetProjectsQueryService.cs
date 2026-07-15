using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectsQueryService(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository)
{
    public ProjectListView Get(Guid currentUserId)
    {
        var activeTaskCountsByProjectId = taskRepository.GetAll()
            .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
            .Where(task => task.IsInFlow)
            .Where(task => string.Equals(task.CurrentState.Key, "active", StringComparison.OrdinalIgnoreCase))
            .GroupBy(task => task.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        return CoreFlowReadModelFactory.CreateProjectList(
            projectRepository.GetAll()
                .Where(project => project.IsAccessibleBy(currentUserId))
                .Select(project => new ProjectListItemView(
                    project.Id,
                    project.Name,
                    project.UpdatedAt,
                    project.IsOwnedBy(currentUserId) ? "專案擁有者" : "專案成員",
                    activeTaskCountsByProjectId.GetValueOrDefault(project.Id)))
                .ToArray());
    }
}
