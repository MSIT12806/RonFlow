using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectBoardQueryService(ICoreFlowReadStore readStore)
{
    public ProjectBoardView? Get(Guid projectId)
    {
        var board = readStore.GetProjectBoard(projectId);
        return board is null ? null : CoreFlowReadModelFactory.CreateProjectBoard(board);
    }
}
