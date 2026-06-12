using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace RonFlow.Infrastructure;

public sealed class DatabaseSyncOptions
{
    public bool Enabled { get; init; }

    public string RuntimeDatabasePath { get; init; } = string.Empty;

    public string RepositoryPath { get; init; } = string.Empty;

    public string? RemoteUrl { get; init; }

    public string Branch { get; init; } = "main";

    public string DatabaseFileName { get; init; } = "ronflow.db";
}

public interface IDatabaseSyncCoordinator
{
    void PullBeforeOpen();

    void PushAfterMutation(string reason);
}

public sealed class NoOpDatabaseSyncCoordinator : IDatabaseSyncCoordinator
{
    public static NoOpDatabaseSyncCoordinator Instance { get; } = new();

    private NoOpDatabaseSyncCoordinator()
    {
    }

    public void PullBeforeOpen()
    {
    }

    public void PushAfterMutation(string reason)
    {
    }
}

public sealed class DatabaseSyncCoordinator(
    DatabaseSyncOptions options,
    IDatabaseSnapshotStore snapshotStore,
    IDatabaseRepositorySync repositorySync) : IDatabaseSyncCoordinator
{
    private readonly object syncRoot = new();

    public void PullBeforeOpen()
    {
        if (!options.Enabled)
        {
            return;
        }

        lock (syncRoot)
        {
            TryRun(() =>
            {
                repositorySync.EnsureReady();
                repositorySync.Pull();

                var repositoryDatabasePath = GetRepositoryDatabasePath();
                if (File.Exists(repositoryDatabasePath))
                {
                    snapshotStore.RestoreSnapshot(repositoryDatabasePath, options.RuntimeDatabasePath);
                }
            });
        }
    }

    public void PushAfterMutation(string reason)
    {
        if (!options.Enabled)
        {
            return;
        }

        lock (syncRoot)
        {
            TryRun(() =>
            {
                repositorySync.EnsureReady();
                snapshotStore.WriteSnapshot(options.RuntimeDatabasePath, GetRepositoryDatabasePath());
                repositorySync.CommitAndPush(options.DatabaseFileName, CreateCommitMessage(reason));
            });
        }
    }

    private string GetRepositoryDatabasePath()
    {
        return Path.Combine(options.RepositoryPath, options.DatabaseFileName);
    }

    private static string CreateCommitMessage(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "Sync RonFlow database"
            : $"Sync RonFlow database: {reason}";
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Sync must not make a successful local persistence mutation fail.
        }
    }
}

public interface IDatabaseSnapshotStore
{
    void RestoreSnapshot(string snapshotPath, string runtimeDatabasePath);

    void WriteSnapshot(string runtimeDatabasePath, string snapshotPath);
}

public sealed class SqliteDatabaseSnapshotStore : IDatabaseSnapshotStore
{
    public void RestoreSnapshot(string snapshotPath, string runtimeDatabasePath)
    {
        EnsureParentDirectory(runtimeDatabasePath);
        File.Copy(snapshotPath, runtimeDatabasePath, overwrite: true);
    }

    public void WriteSnapshot(string runtimeDatabasePath, string snapshotPath)
    {
        if (!File.Exists(runtimeDatabasePath))
        {
            return;
        }

        EnsureParentDirectory(snapshotPath);

        var tempSnapshotPath = snapshotPath + ".tmp";
        if (File.Exists(tempSnapshotPath))
        {
            File.Delete(tempSnapshotPath);
        }

        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = runtimeDatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = tempSnapshotPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        using (var source = new SqliteConnection(sourceConnectionString))
        using (var destination = new SqliteConnection(destinationConnectionString))
        {
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }

        File.Move(tempSnapshotPath, snapshotPath, overwrite: true);
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

public interface IDatabaseRepositorySync
{
    void EnsureReady();

    void Pull();

    void CommitAndPush(string relativePath, string message);
}

public sealed class GitDatabaseRepositorySync(DatabaseSyncOptions options) : IDatabaseRepositorySync
{
    public void EnsureReady()
    {
        if (Directory.Exists(Path.Combine(options.RepositoryPath, ".git")))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.RemoteUrl))
        {
            EnsureParentDirectory(options.RepositoryPath);
            RunGit(Path.GetDirectoryName(Path.GetFullPath(options.RepositoryPath))!, "clone", "--branch", options.Branch, options.RemoteUrl, options.RepositoryPath);
            return;
        }

        Directory.CreateDirectory(options.RepositoryPath);
        RunGit(options.RepositoryPath, "init", "--initial-branch", options.Branch);
    }

    public void Pull()
    {
        if (HasRemote())
        {
            RunGit(options.RepositoryPath, "pull", "--ff-only", "origin", options.Branch);
        }
    }

    public void CommitAndPush(string relativePath, string message)
    {
        RunGit(options.RepositoryPath, "add", relativePath);

        var status = RunGit(options.RepositoryPath, "status", "--porcelain", "--", relativePath);
        if (string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            return;
        }

        RunGit(options.RepositoryPath, "commit", "-m", message);

        if (HasRemote())
        {
            RunGit(options.RepositoryPath, "push", "origin", options.Branch);
        }
    }

    private bool HasRemote()
    {
        return RunGit(options.RepositoryPath, "remote").StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("origin", StringComparer.OrdinalIgnoreCase);
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static GitResult RunGit(string workingDirectory, params string[] arguments)
    {
        var processStartInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start git.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {standardError}");
        }

        return new GitResult(standardOutput, standardError);
    }

    private sealed record GitResult(string StandardOutput, string StandardError);
}
