using System.Diagnostics;
using DbMerger.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

namespace RonFlow.Infrastructure;

public sealed class DatabaseSyncOptions
{
    private const int DefaultGitCommandTimeoutSeconds = 30;

    public bool Enabled { get; init; }

    public string RuntimeDatabasePath { get; init; } = string.Empty;

    public string RepositoryPath { get; init; } = string.Empty;

    public string? RemoteUrl { get; init; }

    public string? AccessToken { get; init; }

    public string Branch { get; init; } = "main";

    public string DatabaseFileName { get; init; } = "ronflow.db";

    public int GitCommandTimeoutSeconds { get; init; } = DefaultGitCommandTimeoutSeconds;

    public TimeSpan GitCommandTimeout => TimeSpan.FromSeconds(Math.Max(1, GitCommandTimeoutSeconds));
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
    IDatabaseRepositorySync repositorySync,
    IDatabaseSnapshotMerger snapshotMerger,
    ILogger<DatabaseSyncCoordinator>? logger = null) : IDatabaseSyncCoordinator
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
            TryRun("synchronize database snapshot before opening runtime database", () =>
            {
                repositorySync.EnsureReady();
                var localSnapshotPath = TryCreateRuntimeSnapshot();
                repositorySync.Pull();

                var repositoryDatabasePath = GetRepositoryDatabasePath();
                if (localSnapshotPath is not null && File.Exists(repositoryDatabasePath))
                {
                    var mergedSnapshotPath = CreateTemporarySnapshotPath("merged");
                    var mergeResult = snapshotMerger.Merge(localSnapshotPath, repositoryDatabasePath, mergedSnapshotPath);
                    if (!mergeResult.Succeeded)
                    {
                        throw new InvalidOperationException($"Database snapshot merge failed: {mergeResult.Message}");
                    }

                    snapshotStore.RestoreSnapshot(mergedSnapshotPath, repositoryDatabasePath);
                    repositorySync.Commit(options.DatabaseFileName, CreateCommitMessage("startup local snapshot"));
                    repositorySync.Push();
                    snapshotStore.RestoreSnapshot(mergedSnapshotPath, options.RuntimeDatabasePath);
                    DeleteTemporarySnapshot(localSnapshotPath);
                    DeleteTemporarySnapshot(mergedSnapshotPath);
                    return;
                }

                if (localSnapshotPath is not null)
                {
                    snapshotStore.RestoreSnapshot(localSnapshotPath, repositoryDatabasePath);
                    repositorySync.Commit(options.DatabaseFileName, CreateCommitMessage("startup local snapshot"));
                    repositorySync.Push();
                    DeleteTemporarySnapshot(localSnapshotPath);
                    return;
                }

                RestoreRepositorySnapshotIfExists();
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
            TryRun($"push database snapshot after mutation '{reason}'", () =>
            {
                repositorySync.EnsureReady();
                var localSnapshotPath = TryCreateRuntimeSnapshot();
                if (localSnapshotPath is null)
                {
                    return;
                }

                repositorySync.Pull();

                var repositoryDatabasePath = GetRepositoryDatabasePath();
                if (File.Exists(repositoryDatabasePath))
                {
                    var mergedSnapshotPath = CreateTemporarySnapshotPath("merged");
                    var mergeResult = snapshotMerger.Merge(localSnapshotPath, repositoryDatabasePath, mergedSnapshotPath);
                    if (!mergeResult.Succeeded)
                    {
                        throw new InvalidOperationException($"Database snapshot merge failed: {mergeResult.Message}");
                    }

                    snapshotStore.RestoreSnapshot(mergedSnapshotPath, repositoryDatabasePath);
                    repositorySync.Commit(options.DatabaseFileName, CreateCommitMessage(reason));
                    repositorySync.Push();
                    DeleteTemporarySnapshot(localSnapshotPath);
                    DeleteTemporarySnapshot(mergedSnapshotPath);
                    return;
                }

                snapshotStore.RestoreSnapshot(localSnapshotPath, repositoryDatabasePath);
                repositorySync.Commit(options.DatabaseFileName, CreateCommitMessage(reason));
                repositorySync.Push();
                DeleteTemporarySnapshot(localSnapshotPath);
            });
        }
    }

    private string? TryCreateRuntimeSnapshot()
    {
        if (!File.Exists(options.RuntimeDatabasePath))
        {
            return null;
        }

        var snapshotPath = CreateTemporarySnapshotPath("local");
        snapshotStore.WriteSnapshot(options.RuntimeDatabasePath, snapshotPath);
        return snapshotPath;
    }

    private void RestoreRepositorySnapshotIfExists()
    {
        var repositoryDatabasePath = GetRepositoryDatabasePath();
        if (File.Exists(repositoryDatabasePath))
        {
            snapshotStore.RestoreSnapshot(repositoryDatabasePath, options.RuntimeDatabasePath);
        }
    }

    private string GetRepositoryDatabasePath()
    {
        return Path.Combine(options.RepositoryPath, options.DatabaseFileName);
    }

    private string CreateTemporarySnapshotPath(string suffix)
    {
        var runtimeDirectory = Path.GetDirectoryName(Path.GetFullPath(options.RuntimeDatabasePath));
        var directory = string.IsNullOrWhiteSpace(runtimeDirectory)
            ? Path.GetFullPath(options.RepositoryPath)
            : runtimeDirectory;

        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"database-git-sync-{suffix}-{Guid.NewGuid():N}.db");
    }

    private static void DeleteTemporarySnapshot(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary cleanup must not hide sync results or failures.
        }
    }

    private static string CreateCommitMessage(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "Sync RonFlow database"
            : $"Sync RonFlow database: {reason}";
    }

    private void TryRun(string operation, Action action)
    {
        WriteDiagnosticLog($"Starting: {operation}");
        try
        {
            action();
            WriteDiagnosticLog($"Completed: {operation}");
        }
        catch (Exception exception)
        {
            // Sync must not make a successful local persistence mutation fail.
            WriteDiagnosticLog($"Failed: {operation}{Environment.NewLine}{exception}");
            logger?.LogWarning(
                exception,
                "RonFlow database Git sync failed while trying to {Operation}. RuntimeDatabasePath: {RuntimeDatabasePath}; RepositoryPath: {RepositoryPath}; RemoteUrl: {RemoteUrl}; Branch: {Branch}; DatabaseFileName: {DatabaseFileName}",
                operation,
                options.RuntimeDatabasePath,
                options.RepositoryPath,
                RedactSensitiveText(options.RemoteUrl),
                options.Branch,
                options.DatabaseFileName);
        }
    }

    private void WriteDiagnosticLog(string message)
    {
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(Path.GetFullPath(options.RuntimeDatabasePath));
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                return;
            }

            Directory.CreateDirectory(runtimeDirectory);
            var logPath = Path.Combine(runtimeDirectory, "database-git-sync.log");
            var line = $"[{DateTimeOffset.UtcNow:O}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // Diagnostic logging must not affect local persistence.
        }
    }

    private static string? RedactSensitiveText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = System.Text.RegularExpressions.Regex.Replace(
            value,
            @"https://[^@\s]+@github\.com",
            "https://***@github.com",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return System.Text.RegularExpressions.Regex.Replace(
            redacted,
            @"github_pat_[A-Za-z0-9_]+",
            "github_pat_***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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

    void Commit(string relativePath, string message);

    void Push();
}

public interface IDatabaseSnapshotMerger
{
    DatabaseSnapshotMergeResult Merge(string localSnapshotPath, string remoteSnapshotPath, string outputSnapshotPath);
}

public sealed record DatabaseSnapshotMergeResult(bool Succeeded, string Message)
{
    public static DatabaseSnapshotMergeResult Success(string message)
    {
        return new DatabaseSnapshotMergeResult(true, message);
    }

    public static DatabaseSnapshotMergeResult Failed(string message)
    {
        return new DatabaseSnapshotMergeResult(false, message);
    }
}

public sealed class DbMergerDatabaseSnapshotMerger : IDatabaseSnapshotMerger
{
    private readonly DbMergeService mergeService = new();

    public DatabaseSnapshotMergeResult Merge(string localSnapshotPath, string remoteSnapshotPath, string outputSnapshotPath)
    {
        var result = mergeService.Merge(new DbMergeRequest(
            localSnapshotPath,
            remoteSnapshotPath,
            outputSnapshotPath,
            DbMergeRecipeIds.RonFlow,
            ConflictResolutionPolicy.LocalWin()));

        return result.Status == DbMergeStatus.Succeeded
            ? DatabaseSnapshotMergeResult.Success($"DbMerger completed with {result.Report.ConflictEntries.Count} conflicts.")
            : DatabaseSnapshotMergeResult.Failed(result.ErrorMessage ?? "DbMerger failed.");
    }
}

public sealed class GitDatabaseRepositorySync(DatabaseSyncOptions options) : IDatabaseRepositorySync
{
    private const string CommitAuthorName = "RonFlow DB Sync";
    private const string CommitAuthorEmail = "ronflow-db-sync@localhost";

    public void EnsureReady()
    {
        if (Directory.Exists(Path.Combine(options.RepositoryPath, ".git")))
        {
            EnsureCommitIdentity();
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.RemoteUrl))
        {
            EnsureParentDirectory(options.RepositoryPath);
            RunGit(Path.GetDirectoryName(Path.GetFullPath(options.RepositoryPath))!, options.GitCommandTimeout, "clone", "--branch", options.Branch, GetRemoteUrlForGitCommand()!, options.RepositoryPath);
            RunGit(options.RepositoryPath, options.GitCommandTimeout, "remote", "set-url", "origin", options.RemoteUrl);
            EnsureCommitIdentity();
            return;
        }

        Directory.CreateDirectory(options.RepositoryPath);
        RunGit(options.RepositoryPath, options.GitCommandTimeout, "init", "--initial-branch", options.Branch);
        EnsureCommitIdentity();
    }

    public void Pull()
    {
        var remoteUrl = GetRemoteUrlForGitCommand();
        if (!string.IsNullOrWhiteSpace(remoteUrl))
        {
            RunGit(options.RepositoryPath, options.GitCommandTimeout, "pull", "--no-rebase", "--no-edit", remoteUrl, options.Branch);
        }
        else if (HasRemote())
        {
            RunGit(options.RepositoryPath, options.GitCommandTimeout, "pull", "--no-rebase", "--no-edit", "origin", options.Branch);
        }
    }

    public void Commit(string relativePath, string message)
    {
        RunGit(options.RepositoryPath, options.GitCommandTimeout, "add", relativePath);

        var status = RunGit(options.RepositoryPath, options.GitCommandTimeout, "status", "--porcelain", "--", relativePath);
        if (string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            return;
        }

        RunGit(options.RepositoryPath, options.GitCommandTimeout, "commit", "-m", message);
    }

    public void Push()
    {
        var remoteUrl = GetRemoteUrlForGitCommand();
        if (!string.IsNullOrWhiteSpace(remoteUrl))
        {
            RunGit(options.RepositoryPath, options.GitCommandTimeout, "push", remoteUrl, options.Branch);
        }
        else if (HasRemote())
        {
            RunGit(options.RepositoryPath, options.GitCommandTimeout, "push", "origin", options.Branch);
        }
    }

    private bool HasRemote()
    {
        return RunGit(options.RepositoryPath, options.GitCommandTimeout, "remote").StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("origin", StringComparer.OrdinalIgnoreCase);
    }

    private string? GetRemoteUrlForGitCommand()
    {
        if (string.IsNullOrWhiteSpace(options.RemoteUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return options.RemoteUrl;
        }

        if (!Uri.TryCreate(options.RemoteUrl, UriKind.Absolute, out var remoteUri) ||
            !string.Equals(remoteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return options.RemoteUrl;
        }

        var builder = new UriBuilder(remoteUri)
        {
            UserName = "x-access-token",
            Password = options.AccessToken,
        };

        return builder.Uri.AbsoluteUri;
    }

    private void EnsureCommitIdentity()
    {
        RunGit(options.RepositoryPath, options.GitCommandTimeout, "config", "user.name", CommitAuthorName);
        RunGit(options.RepositoryPath, options.GitCommandTimeout, "config", "user.email", CommitAuthorEmail);
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static GitResult RunGit(string workingDirectory, TimeSpan timeout, params string[] arguments)
    {
        var processStartInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        processStartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        processStartInfo.Environment["GCM_INTERACTIVE"] = "Never";
        processStartInfo.Environment["GCM_MODAL_PROMPT"] = "false";

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start git.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"git {FormatGitArguments(arguments)} timed out after {timeout.TotalSeconds:0} seconds.");
        }

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {FormatGitArguments(arguments)} failed: {RedactSensitiveText(standardError)}");
        }

        return new GitResult(standardOutput, standardError);
    }

    private static string FormatGitArguments(IEnumerable<string> arguments)
    {
        return RedactSensitiveText(string.Join(' ', arguments));
    }

    private static string RedactSensitiveText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = System.Text.RegularExpressions.Regex.Replace(
            value,
            @"https://[^@\s]+@github\.com",
            "https://***@github.com",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return System.Text.RegularExpressions.Regex.Replace(
            redacted,
            @"github_pat_[A-Za-z0-9_]+",
            "github_pat_***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private sealed record GitResult(string StandardOutput, string StandardError);
}
