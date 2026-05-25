using System.Diagnostics;
using RonFlow.Domain;

namespace RonFlow.Observability;

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
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var operationName = ObservedOperationNames.BoardRead;
            if (ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
            {
                timingSnapshot!.StoreElapsedMs = elapsedMs;
                operationName = timingSnapshot.OperationName;
            }

            RonFlowObservabilityMetrics.RecordStoreDuration(operationName, elapsedMs);
        }
    }

    public TaskModel? GetTaskDetail(Guid projectId, Guid taskId)
    {
        return inner.GetTaskDetail(projectId, taskId);
    }
}