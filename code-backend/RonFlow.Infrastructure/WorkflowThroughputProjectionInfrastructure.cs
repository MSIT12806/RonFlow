using RonFlow.Application;

namespace RonFlow.Infrastructure;

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

public sealed class SqliteWorkflowThroughputProjectionOutbox(SqliteCoreFlowStore store) : IWorkflowThroughputProjectionOutbox
{
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
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT MessageId, ProjectId, TaskId, EventType, StateKey, OccurredAt, ProcessedAt
FROM WorkflowThroughputOutbox
WHERE ProcessedAt IS NULL
ORDER BY OccurredAt";

        using var reader = command.ExecuteReader();
        var items = new List<WorkflowThroughputProjectionSource>();
        while (reader.Read())
        {
            items.Add(new WorkflowThroughputProjectionSource(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6))));
        }

        return items;
    }

    public void MarkProcessed(Guid messageId, DateTimeOffset processedAt)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE WorkflowThroughputOutbox SET ProcessedAt = $processedAt WHERE MessageId = $messageId";
        command.Parameters.AddWithValue("$processedAt", processedAt.ToString("O"));
        command.Parameters.AddWithValue("$messageId", messageId.ToString());
        command.ExecuteNonQuery();
    }

    private void Add(WorkflowThroughputProjectionSource source)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO WorkflowThroughputOutbox (MessageId, ProjectId, TaskId, EventType, StateKey, OccurredAt, ProcessedAt)
VALUES ($messageId, $projectId, $taskId, $eventType, $stateKey, $occurredAt, NULL)";
        command.Parameters.AddWithValue("$messageId", source.MessageId.ToString());
        command.Parameters.AddWithValue("$projectId", source.ProjectId.ToString());
        command.Parameters.AddWithValue("$taskId", source.TaskId.ToString());
        command.Parameters.AddWithValue("$eventType", source.EventType);
        command.Parameters.AddWithValue("$stateKey", (object?)source.StateKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$occurredAt", source.OccurredAt.ToString("O"));
        command.ExecuteNonQuery();
    }
}

public sealed class SqliteWorkflowThroughputProjectionStore(SqliteCoreFlowStore store) : IWorkflowThroughputProjectionStore
{
    public void Apply(WorkflowThroughputProjectionSource source, ReportingBucketType bucketType, DateTimeOffset processedAt)
    {
        var bucketStart = GetBucketStart(source.OccurredAt, bucketType).ToString("yyyy-MM-dd");
        using var connection = store.OpenConnection();
        using var ensureCommand = connection.CreateCommand();
        ensureCommand.CommandText = @"
INSERT INTO WorkflowThroughputBuckets (
    ProjectId,
    BucketType,
    BucketStart,
    CreatedCount,
    MovedToActiveCount,
    MovedToReviewCount,
    CompletedCount,
    ReopenedCount,
    LastUpdatedAt)
VALUES ($projectId, $bucketType, $bucketStart, 0, 0, 0, 0, 0, $lastUpdatedAt)
ON CONFLICT(ProjectId, BucketType, BucketStart) DO NOTHING";
        ensureCommand.Parameters.AddWithValue("$projectId", source.ProjectId.ToString());
        ensureCommand.Parameters.AddWithValue("$bucketType", bucketType.ToContractValue());
        ensureCommand.Parameters.AddWithValue("$bucketStart", bucketStart);
        ensureCommand.Parameters.AddWithValue("$lastUpdatedAt", processedAt.ToString("O"));
        ensureCommand.ExecuteNonQuery();

        var counterColumn = source.EventType switch
        {
            "TaskCreated" => "CreatedCount",
            "TaskCompleted" => "CompletedCount",
            "TaskReopened" => "ReopenedCount",
            "TaskStateChanged" when string.Equals(source.StateKey, "active", StringComparison.OrdinalIgnoreCase) => "MovedToActiveCount",
            "TaskStateChanged" when string.Equals(source.StateKey, "review", StringComparison.OrdinalIgnoreCase) => "MovedToReviewCount",
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(counterColumn))
        {
            using var timestampOnlyCommand = connection.CreateCommand();
            timestampOnlyCommand.CommandText = @"
UPDATE WorkflowThroughputBuckets
SET LastUpdatedAt = $lastUpdatedAt
WHERE ProjectId = $projectId AND BucketType = $bucketType AND BucketStart = $bucketStart";
            timestampOnlyCommand.Parameters.AddWithValue("$lastUpdatedAt", processedAt.ToString("O"));
            timestampOnlyCommand.Parameters.AddWithValue("$projectId", source.ProjectId.ToString());
            timestampOnlyCommand.Parameters.AddWithValue("$bucketType", bucketType.ToContractValue());
            timestampOnlyCommand.Parameters.AddWithValue("$bucketStart", bucketStart);
            timestampOnlyCommand.ExecuteNonQuery();
            return;
        }

        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = $@"
UPDATE WorkflowThroughputBuckets
SET {counterColumn} = {counterColumn} + 1,
    LastUpdatedAt = $lastUpdatedAt
WHERE ProjectId = $projectId AND BucketType = $bucketType AND BucketStart = $bucketStart";
        updateCommand.Parameters.AddWithValue("$lastUpdatedAt", processedAt.ToString("O"));
        updateCommand.Parameters.AddWithValue("$projectId", source.ProjectId.ToString());
        updateCommand.Parameters.AddWithValue("$bucketType", bucketType.ToContractValue());
        updateCommand.Parameters.AddWithValue("$bucketStart", bucketStart);
        updateCommand.ExecuteNonQuery();
    }

    public WorkflowThroughputReportView GetReport(Guid projectId, ReportingBucketType bucketType)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT BucketStart, CreatedCount, MovedToActiveCount, MovedToReviewCount, CompletedCount, ReopenedCount, LastUpdatedAt
FROM WorkflowThroughputBuckets
WHERE ProjectId = $projectId AND BucketType = $bucketType
ORDER BY BucketStart";
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        command.Parameters.AddWithValue("$bucketType", bucketType.ToContractValue());

        using var reader = command.ExecuteReader();
        var buckets = new List<WorkflowThroughputBucketView>();
        DateTimeOffset? lastUpdatedAt = null;
        while (reader.Read())
        {
            buckets.Add(new WorkflowThroughputBucketView(
                DateOnly.Parse(reader.GetString(0)),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5)));

            var candidateUpdatedAt = DateTimeOffset.Parse(reader.GetString(6));
            if (lastUpdatedAt is null || candidateUpdatedAt > lastUpdatedAt)
            {
                lastUpdatedAt = candidateUpdatedAt;
            }
        }

        return new WorkflowThroughputReportView(projectId, bucketType.ToContractValue(), lastUpdatedAt, buckets);
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
}