using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectBoardQueryService(IProjectRepository projectRepository)
{
    public ProjectBoardView? Get(Guid projectId)
    {
        var board = projectRepository.GetBoard(projectId);
        return board is null ? null : CoreFlowReadModelFactory.CreateProjectBoard(board);
    }
}
