using RonFlow.Infrastructure;
using Microsoft.Data.Sqlite;

namespace RonFlow.Infrastructure.Tests;

public sealed class DatabaseSyncCoordinatorTests
{
    [Test]
    public void PullBeforeOpen_WhenRuntimeAndRepositoryDatabasesExist_PullsMergesCommitsPushesAndRestoresMergedSnapshot()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositoryDatabasePath = Path.Combine(repositoryPath, "ronflow.db");
        Directory.CreateDirectory(repositoryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeDatabasePath)!);
        File.WriteAllText(runtimeDatabasePath, "runtime snapshot");
        File.WriteAllText(repositoryDatabasePath, "snapshot");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var snapshotMerger = new RecordingSnapshotMerger();
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync, snapshotMerger);

        coordinator.PullBeforeOpen();

        Assert.That(repositorySync.Calls, Is.EqualTo(["EnsureReady", "Pull", "Commit:ronflow.db:Sync RonFlow database: startup local snapshot", "Push"]));
        Assert.That(snapshotStore.WrittenSnapshots.Single().RuntimeDatabasePath, Is.EqualTo(runtimeDatabasePath));
        Assert.That(snapshotMerger.Merges, Has.Count.EqualTo(1));
        Assert.That(snapshotMerger.Merges.Single().RemoteSnapshotPath, Is.EqualTo(repositoryDatabasePath));
        Assert.That(snapshotStore.RestoredSnapshots, Has.Count.EqualTo(2));
        Assert.That(snapshotStore.RestoredSnapshots[0].RuntimeDatabasePath, Is.EqualTo(repositoryDatabasePath));
        Assert.That(snapshotStore.RestoredSnapshots[1].RuntimeDatabasePath, Is.EqualTo(runtimeDatabasePath));
    }

    [Test]
    public void PullBeforeOpen_WhenRuntimeDatabaseDoesNotExist_PullsPushesAndRestoresRepositorySnapshot()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositoryDatabasePath = Path.Combine(repositoryPath, "ronflow.db");
        Directory.CreateDirectory(repositoryPath);
        File.WriteAllText(repositoryDatabasePath, "snapshot");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync);

        coordinator.PullBeforeOpen();

        Assert.That(repositorySync.Calls, Is.EqualTo(["EnsureReady", "Pull"]));
        Assert.That(snapshotStore.WrittenSnapshots, Is.Empty);
        Assert.That(snapshotStore.RestoredSnapshots, Is.EqualTo([(repositoryDatabasePath, runtimeDatabasePath)]));
    }

    [Test]
    public void PushAfterMutation_EnqueuesReasonWithoutRunningRepositorySync()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync);

        coordinator.PushAfterMutation("task updated");

        Assert.That(repositorySync.Calls, Is.Empty);
        Assert.That(snapshotStore.WrittenSnapshots, Is.Empty);
    }

    [Test]
    public void RequestPullIfStale_EnqueuesReasonWithoutRunningRepositorySync()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var timeProvider = new ManualTimeProvider(GetUtcTimeAfterLastUpdate().AddHours(2));
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync, timeProvider: timeProvider);

        coordinator.RequestPullIfStale("request");

        Assert.That(repositorySync.Calls, Is.Empty);
        Assert.That(snapshotStore.WrittenSnapshots, Is.Empty);
    }

    [Test]
    public void FlushPendingPullRequests_WhenRequestQueued_PullsMergesAndUpdatesLastUpdateTime()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositoryDatabasePath = Path.Combine(repositoryPath, "ronflow.db");
        Directory.CreateDirectory(repositoryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeDatabasePath)!);
        File.WriteAllText(runtimeDatabasePath, "runtime snapshot");
        File.WriteAllText(repositoryDatabasePath, "snapshot");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var snapshotMerger = new RecordingSnapshotMerger();
        var now = GetUtcTimeAfterLastUpdate().AddHours(2);
        var timeProvider = new ManualTimeProvider(now);
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync, snapshotMerger, timeProvider);

        coordinator.RequestPullIfStale("HTTP GET /api/projects");
        var processed = coordinator.FlushPendingPullRequests();

        Assert.That(processed, Is.True);
        Assert.That(repositorySync.Calls, Is.EqualTo(["EnsureReady", "Pull", "Commit:ronflow.db:Sync RonFlow database: HTTP GET /api/projects", "Push"]));
        Assert.That(snapshotStore.WrittenSnapshots.Single().RuntimeDatabasePath, Is.EqualTo(runtimeDatabasePath));
        Assert.That(snapshotMerger.Merges.Single().RemoteSnapshotPath, Is.EqualTo(repositoryDatabasePath));
        Assert.That(snapshotStore.RestoredSnapshots, Has.Count.EqualTo(2));
        Assert.That(DatabaseSyncCoordinator.LastUpdateTime, Is.EqualTo(now.UtcDateTime));
    }

    [Test]
    public void RequestPullIfStale_WhenLastUpdateWithinOneHour_DoesNotQueue()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var timeProvider = new ManualTimeProvider(GetUtcTimeAfterLastUpdate().AddHours(2));
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync, timeProvider: timeProvider);
        coordinator.PullBeforeOpen();
        repositorySync.Calls.Clear();

        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(59));
        coordinator.RequestPullIfStale("request");
        var processed = coordinator.FlushPendingPullRequests();

        Assert.That(processed, Is.False);
        Assert.That(repositorySync.Calls, Is.Empty);
    }

    [Test]
    public void FlushPendingMutations_WritesSnapshotCommitsPullsThenPushesDatabaseFile()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositoryDatabasePath = Path.Combine(repositoryPath, "ronflow.db");
        Directory.CreateDirectory(repositoryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeDatabasePath)!);
        File.WriteAllText(runtimeDatabasePath, "runtime snapshot");
        File.WriteAllText(repositoryDatabasePath, "snapshot");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var snapshotMerger = new RecordingSnapshotMerger();
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync, snapshotMerger);

        coordinator.PushAfterMutation("task updated");
        var processed = coordinator.FlushPendingMutations();

        Assert.That(processed, Is.True);
        Assert.That(repositorySync.Calls, Is.EqualTo(["EnsureReady", "Pull", "Commit:ronflow.db:Sync RonFlow database: task updated", "Push"]));
        Assert.That(snapshotStore.WrittenSnapshots.Single().RuntimeDatabasePath, Is.EqualTo(runtimeDatabasePath));
        Assert.That(snapshotMerger.Merges.Single().RemoteSnapshotPath, Is.EqualTo(repositoryDatabasePath));
        Assert.That(snapshotStore.RestoredSnapshots.Single().RuntimeDatabasePath, Is.EqualTo(repositoryDatabasePath));
    }

    [Test]
    public void FlushPendingMutations_WhenInitiatorQueued_PublishesSucceededOperation()
    {
        using var temp = new TempDirectory();
        var initiatorUserId = Guid.NewGuid();
        var operationStore = new InMemoryDatabaseSyncOperationStore();
        var notificationPublisher = new RecordingDatabaseSyncNotificationPublisher();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositoryDatabasePath = Path.Combine(repositoryPath, "ronflow.db");
        Directory.CreateDirectory(repositoryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeDatabasePath)!);
        File.WriteAllText(runtimeDatabasePath, "runtime snapshot");
        File.WriteAllText(repositoryDatabasePath, "snapshot");
        var coordinator = CreateCoordinator(
            repositoryPath,
            runtimeDatabasePath,
            new RecordingSnapshotStore(),
            new RecordingRepositorySync(),
            new RecordingSnapshotMerger(),
            operationStore: operationStore,
            notificationPublisher: notificationPublisher);

        coordinator.PushAfterMutation("task updated", new DatabaseSyncInitiator(initiatorUserId));
        var processed = coordinator.FlushPendingMutations();

        var operation = operationStore.GetForInitiator(initiatorUserId, 10).Single();
        Assert.That(processed, Is.True);
        Assert.That(operation.Status, Is.EqualTo(DatabaseSyncOperationStatus.Succeeded));
        Assert.That(operation.CompletedAt, Is.Not.Null);
        Assert.That(operation.FailureSummary, Is.Null);
        Assert.That(notificationPublisher.PublishedOperations.Single().Id, Is.EqualTo(operation.Id));
    }

    [Test]
    public void FlushPendingMutations_WhenSnapshotCannotBePushed_PublishesFailedOperation()
    {
        using var temp = new TempDirectory();
        var initiatorUserId = Guid.NewGuid();
        var operationStore = new InMemoryDatabaseSyncOperationStore();
        var notificationPublisher = new RecordingDatabaseSyncNotificationPublisher();
        var coordinator = CreateCoordinator(
            Path.Combine(temp.Path, "repo"),
            Path.Combine(temp.Path, "runtime", "ronflow.db"),
            new RecordingSnapshotStore(),
            new RecordingRepositorySync(),
            operationStore: operationStore,
            notificationPublisher: notificationPublisher);

        coordinator.PushAfterMutation("task updated", new DatabaseSyncInitiator(initiatorUserId));
        var processed = coordinator.FlushPendingMutations();

        var operation = operationStore.GetForInitiator(initiatorUserId, 10).Single();
        Assert.That(processed, Is.True);
        Assert.That(operation.Status, Is.EqualTo(DatabaseSyncOperationStatus.Failed));
        Assert.That(operation.CompletedAt, Is.Not.Null);
        Assert.That(operation.FailureSummary, Is.Not.Empty);
        Assert.That(notificationPublisher.PublishedOperations.Single().Id, Is.EqualTo(operation.Id));
    }

    [Test]
    public void FlushPendingMutations_CoalescesQueuedReasonsIntoOneRepositorySync()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var repositoryDatabasePath = Path.Combine(repositoryPath, "ronflow.db");
        Directory.CreateDirectory(repositoryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeDatabasePath)!);
        File.WriteAllText(runtimeDatabasePath, "runtime snapshot");
        File.WriteAllText(repositoryDatabasePath, "snapshot");
        var repositorySync = new RecordingRepositorySync();
        var snapshotStore = new RecordingSnapshotStore();
        var snapshotMerger = new RecordingSnapshotMerger();
        var coordinator = CreateCoordinator(repositoryPath, runtimeDatabasePath, snapshotStore, repositorySync, snapshotMerger);

        coordinator.PushAfterMutation("project updated");
        coordinator.PushAfterMutation("task updated");
        coordinator.PushAfterMutation("task updated");
        var processed = coordinator.FlushPendingMutations();

        Assert.That(processed, Is.True);
        Assert.That(repositorySync.Calls, Is.EqualTo([
            "EnsureReady",
            "Pull",
            "Commit:ronflow.db:Sync RonFlow database: coalesced 3 mutations: project updated, task updated",
            "Push",
        ]));
        Assert.That(snapshotStore.WrittenSnapshots, Has.Count.EqualTo(1));
        Assert.That(snapshotMerger.Merges, Has.Count.EqualTo(1));
    }

    [Test]
    public void FlushPendingMutations_WhenNoPendingReasons_DoesNotRunRepositorySync()
    {
        using var temp = new TempDirectory();
        var repositorySync = new RecordingRepositorySync();
        var coordinator = CreateCoordinator(
            Path.Combine(temp.Path, "repo"),
            Path.Combine(temp.Path, "runtime", "ronflow.db"),
            new RecordingSnapshotStore(),
            repositorySync);

        var processed = coordinator.FlushPendingMutations();

        Assert.That(processed, Is.False);
        Assert.That(repositorySync.Calls, Is.Empty);
    }

    [Test]
    public void FlushPendingMutations_WhenRepositoryFails_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var coordinator = CreateCoordinator(
            Path.Combine(temp.Path, "repo"),
            Path.Combine(temp.Path, "runtime", "ronflow.db"),
            new ThrowingSnapshotStore(),
            new ThrowingRepositorySync());

        coordinator.PushAfterMutation("task updated");

        Assert.DoesNotThrow(() => coordinator.FlushPendingMutations());
    }

    [Test]
    public void DbMergerDatabaseSnapshotMerger_WhenRemoteTaskMutationIsNewer_WritesRemoteTask()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        var localTask = new KeyedJsonRecord("shared-task", """{"id":"shared-task","title":"local","mutationAt":"2026-07-15T01:00:00+00:00"}""");
        var remoteTask = new KeyedJsonRecord("shared-task", """{"id":"shared-task","title":"remote","mutationAt":"2026-07-15T02:00:00+00:00"}""");
        CreateRonFlowCoreDatabase(localPath, projects: [], tasks: [localTask]);
        CreateRonFlowCoreDatabase(remotePath, projects: [], tasks: [remoteTask]);

        var result = new DbMergerDatabaseSnapshotMerger().Merge(localPath, remotePath, outputPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(ReadJsonData(outputPath, "Tasks", "shared-task"), Is.EqualTo(remoteTask.Data));
    }

    [Test]
    public void DbMergerDatabaseSnapshotMerger_WhenTaskMutationAtTies_WritesLocalTask()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        var localTask = new KeyedJsonRecord("shared-task", """{"id":"shared-task","title":"local","mutationAt":"2026-07-15T01:00:00+00:00"}""");
        var remoteTask = new KeyedJsonRecord("shared-task", """{"id":"shared-task","title":"remote","mutationAt":"2026-07-15T01:00:00+00:00"}""");
        CreateRonFlowCoreDatabase(localPath, projects: [], tasks: [localTask]);
        CreateRonFlowCoreDatabase(remotePath, projects: [], tasks: [remoteTask]);

        var result = new DbMergerDatabaseSnapshotMerger().Merge(localPath, remotePath, outputPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(ReadJsonData(outputPath, "Tasks", "shared-task"), Is.EqualTo(localTask.Data));
    }

    [Test]
    public void DbMergerDatabaseSnapshotMerger_WhenLocalProjectMutationIsNewer_WritesLocalProject()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        var localProject = new KeyedJsonRecord("shared-project", """{"id":"shared-project","name":"local","mutationAt":"2026-07-15T03:00:00+00:00"}""");
        var remoteProject = new KeyedJsonRecord("shared-project", """{"id":"shared-project","name":"remote","mutationAt":"2026-07-15T02:00:00+00:00"}""");
        CreateRonFlowCoreDatabase(localPath, projects: [localProject], tasks: []);
        CreateRonFlowCoreDatabase(remotePath, projects: [remoteProject], tasks: []);

        var result = new DbMergerDatabaseSnapshotMerger().Merge(localPath, remotePath, outputPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(ReadJsonData(outputPath, "Projects", "shared-project"), Is.EqualTo(localProject.Data));
    }

    private static DatabaseSyncCoordinator CreateCoordinator(
        string repositoryPath,
        string runtimeDatabasePath,
        IDatabaseSnapshotStore snapshotStore,
        IDatabaseRepositorySync repositorySync,
        TimeProvider? timeProvider = null,
        IDatabaseSyncOperationStore? operationStore = null,
        IDatabaseSyncNotificationPublisher? notificationPublisher = null)
    {
        return new DatabaseSyncCoordinator(
            new DatabaseSyncOptions
            {
                Enabled = true,
                RepositoryPath = repositoryPath,
                RuntimeDatabasePath = runtimeDatabasePath,
            },
            snapshotStore,
            repositorySync,
            new RecordingSnapshotMerger(),
            timeProvider: timeProvider,
            operationStore: operationStore,
            notificationPublisher: notificationPublisher);
    }

    private static DatabaseSyncCoordinator CreateCoordinator(
        string repositoryPath,
        string runtimeDatabasePath,
        IDatabaseSnapshotStore snapshotStore,
        IDatabaseRepositorySync repositorySync,
        IDatabaseSnapshotMerger snapshotMerger,
        TimeProvider? timeProvider = null,
        IDatabaseSyncOperationStore? operationStore = null,
        IDatabaseSyncNotificationPublisher? notificationPublisher = null)
    {
        return new DatabaseSyncCoordinator(
            new DatabaseSyncOptions
            {
                Enabled = true,
                RepositoryPath = repositoryPath,
                RuntimeDatabasePath = runtimeDatabasePath,
            },
            snapshotStore,
            repositorySync,
            snapshotMerger,
            timeProvider: timeProvider,
            operationStore: operationStore,
            notificationPublisher: notificationPublisher);
    }

    private static DateTimeOffset GetUtcTimeAfterLastUpdate()
    {
        var lastUpdateTime = DatabaseSyncCoordinator.LastUpdateTime;
        if (lastUpdateTime == DateTime.MinValue)
        {
            return new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        return new DateTimeOffset(DateTime.SpecifyKind(lastUpdateTime, DateTimeKind.Utc));
    }

    private static void CreateRonFlowCoreDatabase(
        string path,
        IEnumerable<KeyedJsonRecord> projects,
        IEnumerable<KeyedJsonRecord> tasks)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE Projects (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE Tasks (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);";
        command.ExecuteNonQuery();

        InsertKeyedJsonRecords(connection, "Projects", projects);
        InsertKeyedJsonRecords(connection, "Tasks", tasks);
    }

    private static void InsertKeyedJsonRecords(SqliteConnection connection, string tableName, IEnumerable<KeyedJsonRecord> records)
    {
        foreach (var record in records)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = $"INSERT INTO {tableName} (Id, Data) VALUES ($id, $data)";
            insert.Parameters.AddWithValue("$id", record.Id);
            insert.Parameters.AddWithValue("$data", record.Data);
            insert.ExecuteNonQuery();
        }
    }

    private static string ReadJsonData(string path, string tableName, string id)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Data FROM {tableName} WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        return (string)command.ExecuteScalar()!;
    }

    private sealed record KeyedJsonRecord(string Id, string Data);

    private sealed class RecordingSnapshotStore : IDatabaseSnapshotStore
    {
        public List<(string SnapshotPath, string RuntimeDatabasePath)> RestoredSnapshots { get; } = [];

        public List<(string RuntimeDatabasePath, string SnapshotPath)> WrittenSnapshots { get; } = [];

        public void RestoreSnapshot(string snapshotPath, string runtimeDatabasePath)
        {
            RestoredSnapshots.Add((snapshotPath, runtimeDatabasePath));
        }

        public void WriteSnapshot(string runtimeDatabasePath, string snapshotPath)
        {
            WrittenSnapshots.Add((runtimeDatabasePath, snapshotPath));
        }
    }

    private sealed class RecordingRepositorySync : IDatabaseRepositorySync
    {
        public List<string> Calls { get; } = [];

        public void EnsureReady()
        {
            Calls.Add("EnsureReady");
        }

        public void Pull()
        {
            Calls.Add("Pull");
        }

        public void Commit(string relativePath, string message)
        {
            Calls.Add($"Commit:{relativePath}:{message}");
        }

        public void Push()
        {
            Calls.Add("Push");
        }
    }

    private sealed class RecordingSnapshotMerger : IDatabaseSnapshotMerger
    {
        public List<(string LocalSnapshotPath, string RemoteSnapshotPath, string OutputSnapshotPath)> Merges { get; } = [];

        public DatabaseSnapshotMergeResult Merge(string localSnapshotPath, string remoteSnapshotPath, string outputSnapshotPath)
        {
            Merges.Add((localSnapshotPath, remoteSnapshotPath, outputSnapshotPath));
            return DatabaseSnapshotMergeResult.Success("merged");
        }
    }

    private sealed class RecordingDatabaseSyncNotificationPublisher : IDatabaseSyncNotificationPublisher
    {
        public List<DatabaseSyncOperation> PublishedOperations { get; } = [];

        public void Publish(DatabaseSyncNotification notification)
        {
            PublishedOperations.Add(notification.Operation);
        }
    }

    private sealed class ThrowingSnapshotStore : IDatabaseSnapshotStore
    {
        public void RestoreSnapshot(string snapshotPath, string runtimeDatabasePath)
        {
            throw new InvalidOperationException("Snapshot failed.");
        }

        public void WriteSnapshot(string runtimeDatabasePath, string snapshotPath)
        {
            throw new InvalidOperationException("Snapshot failed.");
        }
    }

    private sealed class ThrowingRepositorySync : IDatabaseRepositorySync
    {
        public void EnsureReady()
        {
            throw new InvalidOperationException("Repository failed.");
        }

        public void Pull()
        {
            throw new InvalidOperationException("Repository failed.");
        }

        public void Commit(string relativePath, string message)
        {
            throw new InvalidOperationException("Repository failed.");
        }

        public void Push()
        {
            throw new InvalidOperationException("Repository failed.");
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return currentUtcNow;
        }

        public void SetUtcNow(DateTimeOffset value)
        {
            currentUtcNow = value;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronflow-db-sync-coordinator-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string DatabasePath(string fileName)
        {
            return System.IO.Path.Combine(Path, fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
