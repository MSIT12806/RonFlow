using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectsQueryService(IProjectRepository projectRepository)
{
    public ProjectListView Get(Guid currentUserId)
    {
        return CoreFlowReadModelFactory.CreateProjectList(
            projectRepository.GetAll()
                .Where(project => project.IsAccessibleBy(currentUserId))
                .Select(project => new ProjectListItemView(
                    project.Id,
                    project.Name,
                    project.UpdatedAt,
                    project.IsOwnedBy(currentUserId) ? "專案擁有者" : "專案成員"))
                .ToArray());
    }
}
