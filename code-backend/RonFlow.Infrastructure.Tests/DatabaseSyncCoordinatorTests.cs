using RonFlow.Infrastructure;

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
    public void PushAfterMutation_WritesSnapshotCommitsPullsThenPushesDatabaseFile()
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

        Assert.That(repositorySync.Calls, Is.EqualTo(["EnsureReady", "Pull", "Commit:ronflow.db:Sync RonFlow database: task updated", "Push"]));
        Assert.That(snapshotStore.WrittenSnapshots.Single().RuntimeDatabasePath, Is.EqualTo(runtimeDatabasePath));
        Assert.That(snapshotMerger.Merges.Single().RemoteSnapshotPath, Is.EqualTo(repositoryDatabasePath));
        Assert.That(snapshotStore.RestoredSnapshots.Single().RuntimeDatabasePath, Is.EqualTo(repositoryDatabasePath));
    }

    [Test]
    public void PushAfterMutation_WhenRepositoryFails_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var coordinator = CreateCoordinator(
            Path.Combine(temp.Path, "repo"),
            Path.Combine(temp.Path, "runtime", "ronflow.db"),
            new ThrowingSnapshotStore(),
            new ThrowingRepositorySync());

        Assert.DoesNotThrow(() => coordinator.PushAfterMutation("task updated"));
    }

    private static DatabaseSyncCoordinator CreateCoordinator(
        string repositoryPath,
        string runtimeDatabasePath,
        IDatabaseSnapshotStore snapshotStore,
        IDatabaseRepositorySync repositorySync)
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
            new RecordingSnapshotMerger());
    }

    private static DatabaseSyncCoordinator CreateCoordinator(
        string repositoryPath,
        string runtimeDatabasePath,
        IDatabaseSnapshotStore snapshotStore,
        IDatabaseRepositorySync repositorySync,
        IDatabaseSnapshotMerger snapshotMerger)
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
            snapshotMerger);
    }

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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronflow-db-sync-coordinator-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
