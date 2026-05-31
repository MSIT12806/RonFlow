using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectCodeTraceabilityQueryService(
    ProjectAccessService projectAccessService,
    ICoreFlowReadStore readStore)
{
    public OwnedResourceQueryResult<ProjectCodeTraceabilityView> Get(Guid currentUserId, Guid projectId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<ProjectCodeTraceabilityView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<ProjectCodeTraceabilityView>.Denied();
        }

        var board = readStore.GetProjectBoard(projectId);
        return board is null
            ? OwnedResourceQueryResult<ProjectCodeTraceabilityView>.Missing()
            : OwnedResourceQueryResult<ProjectCodeTraceabilityView>.Success(CoreFlowReadModelFactory.CreateProjectCodeTraceability(board));
    }
}