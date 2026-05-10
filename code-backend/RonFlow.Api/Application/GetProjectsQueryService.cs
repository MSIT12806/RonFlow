using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectsQueryService(IProjectRepository projectRepository)
{
    public ProjectListView Get()
    {
        return CoreFlowReadModelFactory.CreateProjectList(projectRepository.GetProjects());
    }
}
