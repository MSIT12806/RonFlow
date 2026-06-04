using RonFlow.Domain;

namespace RonFlow.Application;

public enum ReportingBucketType
{
    Day,
    Week,
}

public static class ReportingBucketTypeParser
{
    public static bool TryParse(string? rawValue, out ReportingBucketType bucketType)
    {
        switch (rawValue?.Trim().ToLowerInvariant())
        {
            case "day":
                bucketType = ReportingBucketType.Day;
                return true;
            case "week":
                bucketType = ReportingBucketType.Week;
                return true;
            default:
                bucketType = ReportingBucketType.Day;
                return false;
        }
    }

    public static string ToContractValue(this ReportingBucketType bucketType)
    {
        return bucketType == ReportingBucketType.Day ? "day" : "week";
    }
}

public sealed record WorkflowThroughputProjectionSource(
    Guid MessageId,
    Guid ProjectId,
    Guid TaskId,
    string EventType,
    string? StateKey,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ProcessedAt);

public sealed record WorkflowThroughputBucketView(
    DateOnly BucketStart,
    int CreatedCount,
    int MovedToActiveCount,
    int MovedToReviewCount,
    int CompletedCount,
    int ReopenedCount);

public sealed record WorkflowThroughputReportView(
    Guid ProjectId,
    string BucketType,
    DateTimeOffset? LastUpdatedAt,
    IReadOnlyList<WorkflowThroughputBucketView> Buckets);

public interface IWorkflowThroughputProjectionOutbox
{
    void EnqueueTaskCreated(Guid projectId, Guid taskId, DateTimeOffset occurredAt);

    void EnqueueTaskStateChanged(Guid projectId, Guid taskId, string stateKey, DateTimeOffset occurredAt);

    void EnqueueTaskCompleted(Guid projectId, Guid taskId, DateTimeOffset occurredAt);

    void EnqueueTaskReopened(Guid projectId, Guid taskId, DateTimeOffset occurredAt);

    IReadOnlyList<WorkflowThroughputProjectionSource> GetPending();

    void MarkProcessed(Guid messageId, DateTimeOffset processedAt);
}

public interface IWorkflowThroughputProjectionStore
{
    void Apply(WorkflowThroughputProjectionSource source, ReportingBucketType bucketType, DateTimeOffset processedAt);

    WorkflowThroughputReportView GetReport(Guid projectId, ReportingBucketType bucketType);
}

public sealed class NoOpWorkflowThroughputProjectionOutbox : IWorkflowThroughputProjectionOutbox
{
    public void EnqueueTaskCreated(Guid projectId, Guid taskId, DateTimeOffset occurredAt)
    {
    }

    public void EnqueueTaskStateChanged(Guid projectId, Guid taskId, string stateKey, DateTimeOffset occurredAt)
    {
    }

    public void EnqueueTaskCompleted(Guid projectId, Guid taskId, DateTimeOffset occurredAt)
    {
    }

    public void EnqueueTaskReopened(Guid projectId, Guid taskId, DateTimeOffset occurredAt)
    {
    }

    public IReadOnlyList<WorkflowThroughputProjectionSource> GetPending()
    {
        return [];
    }

    public void MarkProcessed(Guid messageId, DateTimeOffset processedAt)
    {
    }
}

public sealed class ProcessWorkflowThroughputProjectionService(
    IWorkflowThroughputProjectionOutbox outbox,
    IWorkflowThroughputProjectionStore store,
    TimeProvider timeProvider)
{
    public void ProcessPending()
    {
        foreach (var source in outbox.GetPending())
        {
            var processedAt = timeProvider.GetUtcNow();
            store.Apply(source, ReportingBucketType.Day, processedAt);
            store.Apply(source, ReportingBucketType.Week, processedAt);
            outbox.MarkProcessed(source.MessageId, processedAt);
        }
    }
}

public sealed class GetWorkflowThroughputReportQueryService(
    IProjectRepository projectRepository,
    IWorkflowThroughputProjectionStore store)
{
    public OwnedResourceQueryResult<WorkflowThroughputReportView> Get(Guid currentUserId, Guid projectId, ReportingBucketType bucketType)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return OwnedResourceQueryResult<WorkflowThroughputReportView>.Missing();
        }

        if (!project.IsAccessibleBy(currentUserId))
        {
            return OwnedResourceQueryResult<WorkflowThroughputReportView>.Denied();
        }

        return OwnedResourceQueryResult<WorkflowThroughputReportView>.Success(store.GetReport(projectId, bucketType));
    }
}