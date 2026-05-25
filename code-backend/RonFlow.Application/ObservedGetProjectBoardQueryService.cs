using System.Diagnostics;

namespace RonFlow.Application;

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
            BoardReadObservabilityContext.Current.ApplicationElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
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
            BoardReadObservabilityContext.Current.ApplicationElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        }
    }
}