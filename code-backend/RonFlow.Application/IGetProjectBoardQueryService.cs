namespace RonFlow.Application;

public interface IGetProjectBoardQueryService
{
    ProjectBoardView? Get(Guid projectId);

    OwnedResourceQueryResult<ProjectBoardView> Get(Guid currentUserId, Guid projectId);
}