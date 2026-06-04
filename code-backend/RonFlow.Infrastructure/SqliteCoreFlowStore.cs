using Microsoft.Data.Sqlite;

namespace RonFlow.Infrastructure;

public sealed class SqliteCoreFlowStore
{
    private readonly string connectionString;

    public SqliteCoreFlowStore(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        EnsureInitialized();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureInitialized()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Projects (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Tasks (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PushSubscriptions (
    Endpoint TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS KnownUsers (
    UserId TEXT NOT NULL PRIMARY KEY,
    UserName TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS WorkflowThroughputOutbox (
    MessageId TEXT NOT NULL PRIMARY KEY,
    ProjectId TEXT NOT NULL,
    TaskId TEXT NOT NULL,
    EventType TEXT NOT NULL,
    StateKey TEXT NULL,
    OccurredAt TEXT NOT NULL,
    ProcessedAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS WorkflowThroughputBuckets (
    ProjectId TEXT NOT NULL,
    BucketType TEXT NOT NULL,
    BucketStart TEXT NOT NULL,
    CreatedCount INTEGER NOT NULL,
    MovedToActiveCount INTEGER NOT NULL,
    MovedToReviewCount INTEGER NOT NULL,
    CompletedCount INTEGER NOT NULL,
    ReopenedCount INTEGER NOT NULL,
    LastUpdatedAt TEXT NOT NULL,
    PRIMARY KEY (ProjectId, BucketType, BucketStart)
);

CREATE TABLE IF NOT EXISTS AiAuditOutbox (
    MessageId TEXT NOT NULL PRIMARY KEY,
    AuditEntryId TEXT NOT NULL,
    SessionId TEXT NOT NULL,
    ActorType TEXT NOT NULL,
    ActorIdentity TEXT NOT NULL,
    TargetType TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    RequestedChange TEXT NOT NULL,
    ResultStatus TEXT NOT NULL,
    ActualDiffText TEXT NOT NULL,
    OccurredAt TEXT NOT NULL,
    ProcessedAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS AiAuditReadModel (
    AuditEntryId TEXT NOT NULL PRIMARY KEY,
    SessionId TEXT NOT NULL,
    ActorType TEXT NOT NULL,
    ActorIdentity TEXT NOT NULL,
    TargetType TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    RequestedChange TEXT NOT NULL,
    ResultStatus TEXT NOT NULL,
    ActualDiffText TEXT NOT NULL,
    OccurredAt TEXT NOT NULL,
    ProjectedAt TEXT NOT NULL
);";

        command.ExecuteNonQuery();
    }
}