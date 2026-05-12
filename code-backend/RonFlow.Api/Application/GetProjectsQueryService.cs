using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectsQueryService(ICoreFlowReadStore readStore)
{
    public ProjectListView Get()
    {
        return CoreFlowReadModelFactory.CreateProjectList(readStore.GetProjects());
    }
}
