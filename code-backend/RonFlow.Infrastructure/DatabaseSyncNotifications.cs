using Microsoft.Data.Sqlite;

namespace RonFlow.Infrastructure;

public sealed record DatabaseSyncInitiator(Guid UserId);

public interface IDatabaseSyncInitiatorContext
{
    DatabaseSyncInitiator? GetCurrent();
}

public sealed class NoOpDatabaseSyncInitiatorContext : IDatabaseSyncInitiatorContext
{
    public static NoOpDatabaseSyncInitiatorContext Instance { get; } = new();

    private NoOpDatabaseSyncInitiatorContext()
    {
    }

    public DatabaseSyncInitiator? GetCurrent() => null;
}

public enum DatabaseSyncOperationStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

public sealed record DatabaseSyncOperation(
    Guid Id,
    Guid InitiatorUserId,
    string Reason,
    DatabaseSyncOperationStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureSummary);

public sealed record DatabaseSyncNotification(DatabaseSyncOperation Operation);

public interface IDatabaseSyncOperationStore
{
    DatabaseSyncOperation Create(Guid initiatorUserId, string reason, DateTimeOffset requestedAt);

    void MarkRunning(IEnumerable<Guid> operationIds, DateTimeOffset startedAt);

    IReadOnlyList<DatabaseSyncOperation> MarkCompleted(IEnumerable<Guid> operationIds, bool succeeded, DateTimeOffset completedAt, string? failureSummary);

    IReadOnlyList<DatabaseSyncOperation> GetForInitiator(Guid initiatorUserId, int limit);
}

public interface IDatabaseSyncNotificationPublisher
{
    void Publish(DatabaseSyncNotification notification);
}

public sealed class NoOpDatabaseSyncOperationStore : IDatabaseSyncOperationStore
{
    public static NoOpDatabaseSyncOperationStore Instance { get; } = new();

    private NoOpDatabaseSyncOperationStore()
    {
    }

    public DatabaseSyncOperation Create(Guid initiatorUserId, string reason, DateTimeOffset requestedAt) =>
        new(Guid.NewGuid(), initiatorUserId, reason, DatabaseSyncOperationStatus.Queued, requestedAt, null, null, null);

    public IReadOnlyList<DatabaseSyncOperation> GetForInitiator(Guid initiatorUserId, int limit) => [];

    public void MarkRunning(IEnumerable<Guid> operationIds, DateTimeOffset startedAt)
    {
    }

    public IReadOnlyList<DatabaseSyncOperation> MarkCompleted(IEnumerable<Guid> operationIds, bool succeeded, DateTimeOffset completedAt, string? failureSummary) => [];
}

public sealed class InMemoryDatabaseSyncOperationStore : IDatabaseSyncOperationStore
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, DatabaseSyncOperation> operations = new();

    public DatabaseSyncOperation Create(Guid initiatorUserId, string reason, DateTimeOffset requestedAt)
    {
        var operation = new DatabaseSyncOperation(
            Guid.NewGuid(),
            initiatorUserId,
            reason,
            DatabaseSyncOperationStatus.Queued,
            requestedAt,
            null,
            null,
            null);

        lock (syncRoot)
        {
            operations[operation.Id] = operation;
        }

        return operation;
    }

    public void MarkRunning(IEnumerable<Guid> operationIds, DateTimeOffset startedAt)
    {
        lock (syncRoot)
        {
            foreach (var operationId in operationIds.Distinct())
            {
                if (operations.TryGetValue(operationId, out var operation))
                {
                    operations[operationId] = operation with
                    {
                        Status = DatabaseSyncOperationStatus.Running,
                        StartedAt = startedAt,
                    };
                }
            }
        }
    }

    public IReadOnlyList<DatabaseSyncOperation> MarkCompleted(IEnumerable<Guid> operationIds, bool succeeded, DateTimeOffset completedAt, string? failureSummary)
    {
        var completed = new List<DatabaseSyncOperation>();
        lock (syncRoot)
        {
            foreach (var operationId in operationIds.Distinct())
            {
                if (!operations.TryGetValue(operationId, out var operation))
                {
                    continue;
                }

                var completedOperation = operation with
                {
                    Status = succeeded ? DatabaseSyncOperationStatus.Succeeded : DatabaseSyncOperationStatus.Failed,
                    CompletedAt = completedAt,
                    FailureSummary = failureSummary,
                };
                operations[operationId] = completedOperation;
                completed.Add(completedOperation);
            }
        }

        return completed;
    }

    public IReadOnlyList<DatabaseSyncOperation> GetForInitiator(Guid initiatorUserId, int limit)
    {
        lock (syncRoot)
        {
            return operations.Values
                .Where(operation => operation.InitiatorUserId == initiatorUserId)
                .OrderByDescending(operation => operation.RequestedAt)
                .Take(Math.Clamp(limit, 1, 100))
                .ToArray();
        }
    }
}

public sealed class NoOpDatabaseSyncNotificationPublisher : IDatabaseSyncNotificationPublisher
{
    public static NoOpDatabaseSyncNotificationPublisher Instance { get; } = new();

    private NoOpDatabaseSyncNotificationPublisher()
    {
    }

    public void Publish(DatabaseSyncNotification notification)
    {
    }
}

public sealed class SqliteDatabaseSyncOperationStore : IDatabaseSyncOperationStore
{
    private readonly string connectionString;

    public SqliteDatabaseSyncOperationStore(string databasePath)
    {
        var fullDatabasePath = Path.GetFullPath(databasePath);
        var databaseDirectory = Path.GetDirectoryName(fullDatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        EnsureInitialized();
    }

    public DatabaseSyncOperation Create(Guid initiatorUserId, string reason, DateTimeOffset requestedAt)
    {
        var operation = new DatabaseSyncOperation(Guid.NewGuid(), initiatorUserId, reason, DatabaseSyncOperationStatus.Queued, requestedAt, null, null, null);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO DatabaseSyncOperations (Id, InitiatorUserId, Reason, Status, RequestedAt, StartedAt, CompletedAt, FailureSummary)
VALUES ($id, $initiatorUserId, $reason, $status, $requestedAt, NULL, NULL, NULL);";
        command.Parameters.AddWithValue("$id", operation.Id.ToString());
        command.Parameters.AddWithValue("$initiatorUserId", operation.InitiatorUserId.ToString());
        command.Parameters.AddWithValue("$reason", operation.Reason);
        command.Parameters.AddWithValue("$status", operation.Status.ToString());
        command.Parameters.AddWithValue("$requestedAt", operation.RequestedAt.ToString("O"));
        command.ExecuteNonQuery();
        return operation;
    }

    public void MarkRunning(IEnumerable<Guid> operationIds, DateTimeOffset startedAt)
    {
        foreach (var operationId in operationIds.Distinct())
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE DatabaseSyncOperations SET Status = $status, StartedAt = $startedAt WHERE Id = $id";
            command.Parameters.AddWithValue("$status", DatabaseSyncOperationStatus.Running.ToString());
            command.Parameters.AddWithValue("$startedAt", startedAt.ToString("O"));
            command.Parameters.AddWithValue("$id", operationId.ToString());
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<DatabaseSyncOperation> MarkCompleted(IEnumerable<Guid> operationIds, bool succeeded, DateTimeOffset completedAt, string? failureSummary)
    {
        var completed = new List<DatabaseSyncOperation>();
        foreach (var operationId in operationIds.Distinct())
        {
            using var connection = OpenConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE DatabaseSyncOperations
SET Status = $status, CompletedAt = $completedAt, FailureSummary = $failureSummary
WHERE Id = $id;";
                command.Parameters.AddWithValue("$status", succeeded ? DatabaseSyncOperationStatus.Succeeded.ToString() : DatabaseSyncOperationStatus.Failed.ToString());
                command.Parameters.AddWithValue("$completedAt", completedAt.ToString("O"));
                command.Parameters.AddWithValue("$failureSummary", (object?)failureSummary ?? DBNull.Value);
                command.Parameters.AddWithValue("$id", operationId.ToString());
                command.ExecuteNonQuery();
            }

            var operation = GetById(connection, operationId);
            if (operation is not null)
            {
                completed.Add(operation);
            }
        }

        return completed;
    }

    public IReadOnlyList<DatabaseSyncOperation> GetForInitiator(Guid initiatorUserId, int limit)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT Id, InitiatorUserId, Reason, Status, RequestedAt, StartedAt, CompletedAt, FailureSummary
FROM DatabaseSyncOperations
WHERE InitiatorUserId = $initiatorUserId
ORDER BY RequestedAt DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$initiatorUserId", initiatorUserId.ToString());
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
        using var reader = command.ExecuteReader();
        var operations = new List<DatabaseSyncOperation>();
        while (reader.Read())
        {
            operations.Add(ReadOperation(reader));
        }

        return operations;
    }

    private void EnsureInitialized()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS DatabaseSyncOperations (
    Id TEXT NOT NULL PRIMARY KEY,
    InitiatorUserId TEXT NOT NULL,
    Reason TEXT NOT NULL,
    Status TEXT NOT NULL,
    RequestedAt TEXT NOT NULL,
    StartedAt TEXT NULL,
    CompletedAt TEXT NULL,
    FailureSummary TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_DatabaseSyncOperations_InitiatorUserId_RequestedAt
ON DatabaseSyncOperations (InitiatorUserId, RequestedAt DESC);";
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static DatabaseSyncOperation? GetById(SqliteConnection connection, Guid operationId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT Id, InitiatorUserId, Reason, Status, RequestedAt, StartedAt, CompletedAt, FailureSummary
FROM DatabaseSyncOperations WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", operationId.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadOperation(reader) : null;
    }

    private static DatabaseSyncOperation ReadOperation(SqliteDataReader reader)
    {
        return new DatabaseSyncOperation(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            Enum.Parse<DatabaseSyncOperationStatus>(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture),
            reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), System.Globalization.CultureInfo.InvariantCulture),
            reader.IsDBNull(7) ? null : reader.GetString(7));
    }
}
