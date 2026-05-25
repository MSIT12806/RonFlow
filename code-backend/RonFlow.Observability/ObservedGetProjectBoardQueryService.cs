using System.Diagnostics;
using RonFlow.Application;

namespace RonFlow.Observability;

public sealed class ObservedGetProjectBoardQueryService(IGetProjectBoardQueryService inner) : IGetProjectBoardQueryService
{
    public ProjectBoardView? Get(Guid projectId)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return inner.Get(projectId);
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var operationName = ObservedOperationNames.BoardRead;
            if (ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
            {
                timingSnapshot!.ApplicationElapsedMs = elapsedMs;
                operationName = timingSnapshot.OperationName;
            }

            RonFlowObservabilityMetrics.RecordApplicationDuration(operationName, elapsedMs);
        }
    }

    public OwnedResourceQueryResult<ProjectBoardView> Get(Guid currentUserId, Guid projectId)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return inner.Get(currentUserId, projectId);
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var operationName = ObservedOperationNames.BoardRead;
            if (ObservedOperationTimingContext.TryGetCurrent(out var timingSnapshot))
            {
                timingSnapshot!.ApplicationElapsedMs = elapsedMs;
                operationName = timingSnapshot.OperationName;
            }

            RonFlowObservabilityMetrics.RecordApplicationDuration(operationName, elapsedMs);
        }
    }
}