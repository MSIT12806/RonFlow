using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectsQueryService(ICoreFlowReadStore readStore)
{
    public ProjectListView Get(Guid currentUserId)
    {
        return CoreFlowReadModelFactory.CreateProjectList(
            readStore.GetProjects()
                .Where(project => project.OwnerId == currentUserId)
                .ToArray());
    }
}
