using RonFlow.Application;

namespace RonFlow.Testing.Infrastructure;

public sealed class InMemoryWorkflowThroughputProjectionOutbox : IWorkflowThroughputProjectionOutbox
{
    private readonly object syncRoot = new();
    private readonly List<WorkflowThroughputProjectionSource> pending = [];

    public void EnqueueTaskCreated(Guid projectId, Guid taskId, DateTimeOffset occurredAt)
    {
        Add(new WorkflowThroughputProjectionSource(Guid.NewGuid(), projectId, taskId, "TaskCreated", null, occurredAt, null));
    }

    public void EnqueueTaskStateChanged(Guid projectId, Guid taskId, string stateKey, DateTimeOffset occurredAt)
    {
        Add(new WorkflowThroughputProjectionSource(Guid.NewGuid(), projectId, taskId, "TaskStateChanged", stateKey, occurredAt, null));
    }

    public void EnqueueTaskCompleted(Guid projectId, Guid taskId, DateTimeOffset occurredAt)
    {
        Add(new WorkflowThroughputProjectionSource(Guid.NewGuid(), projectId, taskId, "TaskCompleted", null, occurredAt, null));
    }

    public void EnqueueTaskReopened(Guid projectId, Guid taskId, DateTimeOffset occurredAt)
    {
        Add(new WorkflowThroughputProjectionSource(Guid.NewGuid(), projectId, taskId, "TaskReopened", null, occurredAt, null));
    }

    public IReadOnlyList<WorkflowThroughputProjectionSource> GetPending()
    {
        lock (syncRoot)
        {
            return pending
                .Where(item => item.ProcessedAt is null)
                .OrderBy(item => item.OccurredAt)
                .ToArray();
        }
    }

    public void MarkProcessed(Guid messageId, DateTimeOffset processedAt)
    {
        lock (syncRoot)
        {
            var index = pending.FindIndex(item => item.MessageId == messageId);
            if (index >= 0)
            {
                pending[index] = pending[index] with { ProcessedAt = processedAt };
            }
        }
    }

    private void Add(WorkflowThroughputProjectionSource source)
    {
        lock (syncRoot)
        {
            pending.Add(source);
        }
    }
}

public sealed class InMemoryWorkflowThroughputProjectionStore : IWorkflowThroughputProjectionStore
{
    private readonly object syncRoot = new();
    private readonly Dictionary<(Guid ProjectId, ReportingBucketType BucketType, DateOnly BucketStart), WorkflowThroughputBucketAccumulator> buckets = [];
    private readonly Dictionary<(Guid ProjectId, ReportingBucketType BucketType), DateTimeOffset> lastUpdatedAt = [];

    public void Apply(WorkflowThroughputProjectionSource source, ReportingBucketType bucketType, DateTimeOffset processedAt)
    {
        lock (syncRoot)
        {
            var bucketStart = GetBucketStart(source.OccurredAt, bucketType);
            var key = (source.ProjectId, bucketType, bucketStart);
            if (!buckets.TryGetValue(key, out var accumulator))
            {
                accumulator = new WorkflowThroughputBucketAccumulator();
                buckets[key] = accumulator;
            }

            accumulator.Apply(source);
            lastUpdatedAt[(source.ProjectId, bucketType)] = processedAt;
        }
    }

    public WorkflowThroughputReportView GetReport(Guid projectId, ReportingBucketType bucketType)
    {
        lock (syncRoot)
        {
            var reportBuckets = buckets
                .Where(item => item.Key.ProjectId == projectId && item.Key.BucketType == bucketType)
                .OrderBy(item => item.Key.BucketStart)
                .Select(item => item.Value.ToView(item.Key.BucketStart))
                .ToArray();

            lastUpdatedAt.TryGetValue((projectId, bucketType), out var updatedAt);
            return new WorkflowThroughputReportView(projectId, bucketType.ToContractValue(), updatedAt == default ? null : updatedAt, reportBuckets);
        }
    }

    private static DateOnly GetBucketStart(DateTimeOffset occurredAt, ReportingBucketType bucketType)
    {
        var date = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        if (bucketType == ReportingBucketType.Day)
        {
            return date;
        }

        var dayOfWeek = (int)occurredAt.UtcDateTime.DayOfWeek;
        var mondayOffset = (dayOfWeek + 6) % 7;
        return date.AddDays(-mondayOffset);
    }

    private sealed class WorkflowThroughputBucketAccumulator
    {
        public int CreatedCount { get; private set; }
        public int MovedToActiveCount { get; private set; }
        public int MovedToReviewCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int ReopenedCount { get; private set; }

        public void Apply(WorkflowThroughputProjectionSource source)
        {
            switch (source.EventType)
            {
                case "TaskCreated":
                    CreatedCount += 1;
                    break;
                case "TaskStateChanged" when string.Equals(source.StateKey, "active", StringComparison.OrdinalIgnoreCase):
                    MovedToActiveCount += 1;
                    break;
                case "TaskStateChanged" when string.Equals(source.StateKey, "review", StringComparison.OrdinalIgnoreCase):
                    MovedToReviewCount += 1;
                    break;
                case "TaskCompleted":
                    CompletedCount += 1;
                    break;
                case "TaskReopened":
                    ReopenedCount += 1;
                    break;
            }
        }

        public WorkflowThroughputBucketView ToView(DateOnly bucketStart)
        {
            return new WorkflowThroughputBucketView(bucketStart, CreatedCount, MovedToActiveCount, MovedToReviewCount, CompletedCount, ReopenedCount);
        }
    }
}
