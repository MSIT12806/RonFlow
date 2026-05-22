using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectBoardQueryService(ProjectAccessService projectAccessService, ICoreFlowReadStore readStore)
{
    public GetProjectBoardQueryService(ICoreFlowReadStore readStore)
        : this(null!, readStore)
    {
    }

    public ProjectBoardView? Get(Guid projectId)
    {
        var board = readStore.GetProjectBoard(projectId);
        return board is null ? null : CoreFlowReadModelFactory.CreateProjectBoard(board);
    }

    public OwnedResourceQueryResult<ProjectBoardView> Get(Guid currentUserId, Guid projectId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<ProjectBoardView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<ProjectBoardView>.Denied();
        }

        var board = readStore.GetProjectBoard(projectId);
        return board is null
            ? OwnedResourceQueryResult<ProjectBoardView>.Missing()
            : OwnedResourceQueryResult<ProjectBoardView>.Success(CoreFlowReadModelFactory.CreateProjectBoard(board));
    }
}
