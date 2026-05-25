using System.Diagnostics;
using RonFlow.Application;
using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class ObservedCoreFlowReadStore(ICoreFlowReadStore inner) : ICoreFlowReadStore
{
    public IReadOnlyList<ProjectSummaryModel> GetProjects()
    {
        return inner.GetProjects();
    }

    public ProjectBoardModel? GetProjectBoard(Guid projectId)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return inner.GetProjectBoard(projectId);
        }
        finally
        {
            stopwatch.Stop();
            BoardReadObservabilityContext.Current.StoreElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        }
    }

    public TaskModel? GetTaskDetail(Guid projectId, Guid taskId)
    {
        return inner.GetTaskDetail(projectId, taskId);
    }
}