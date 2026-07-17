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
    void SynchronizeStartupSnapshot();

    void RequestPullIfStale(string reason);

    bool FlushPendingPullRequests();

    void PushAfterMutation(string reason);

    void PushAfterMutation(string reason, DatabaseSyncInitiator? initiator)
    {
        PushAfterMutation(reason);
    }

    bool FlushPendingMutations();
}

public sealed class NoOpDatabaseSyncCoordinator : IDatabaseSyncCoordinator
{
    public static NoOpDatabaseSyncCoordinator Instance { get; } = new();

    private NoOpDatabaseSyncCoordinator()
    {
    }

    public void SynchronizeStartupSnapshot()
    {
    }

    public void RequestPullIfStale(string reason)
    {
    }

    public bool FlushPendingPullRequests()
    {
        return false;
    }

    public void PushAfterMutation(string reason)
    {
    }

    public void PushAfterMutation(string reason, DatabaseSyncInitiator? initiator)
    {
    }

    public bool FlushPendingMutations()
    {
        return false;
    }
}

public sealed class DatabaseSyncCoordinator(
    DatabaseSyncOptions options,
    IDatabaseSnapshotStore snapshotStore,
    IDatabaseRepositorySync repositorySync,
    IDatabaseSnapshotMerger snapshotMerger,
    ILogger<DatabaseSyncCoordinator>? logger = null,
    TimeProvider? timeProvider = null,
    IDatabaseSyncOperationStore? operationStore = null,
    IDatabaseSyncNotificationPublisher? notificationPublisher = null,
    IRuntimeDatabaseAccessGate? runtimeDatabaseAccessGate = null) : IDatabaseSyncCoordinator
{
    private const string UserVisibleFailureSummary = "Git database sync failed. Please check server sync diagnostics.";
    private const string RequestPullReason = "request-triggered pull refresh";
    private static readonly TimeSpan PullRefreshInterval = TimeSpan.FromHours(1);
    private static readonly object lastUpdateTimeRoot = new();
    private static DateTime lastUpdateTime = DateTime.MinValue;

    private readonly object syncRoot = new();
    private readonly object pendingMutationReasonsRoot = new();
    private readonly object pendingPullRequestsRoot = new();
    private readonly Queue<DatabaseSyncMutationRequest> pendingMutationRequests = new();
    private readonly Queue<string> pendingPullReasons = new();
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IDatabaseSyncOperationStore operationStore = operationStore ?? NoOpDatabaseSyncOperationStore.Instance;
    private readonly IDatabaseSyncNotificationPublisher notificationPublisher = notificationPublisher ?? NoOpDatabaseSyncNotificationPublisher.Instance;
    private readonly IRuntimeDatabaseAccessGate runtimeDatabaseAccessGate = runtimeDatabaseAccessGate ?? new RuntimeDatabaseAccessGate();

    public static DateTime LastUpdateTime
    {
        get
        {
            lock (lastUpdateTimeRoot)
            {
                return lastUpdateTime;
            }
        }
    }

    public void SynchronizeStartupSnapshot()
    {
        if (!options.Enabled)
        {
            return;
        }

        lock (syncRoot)
        {
            if (TryRun("synchronize database snapshot before opening runtime database", () =>
            {
                PullAndMergeDatabaseSnapshot("startup local snapshot");
            }))
            {
                MarkLastUpdateTime();
            }
        }
    }

    public void RequestPullIfStale(string reason)
    {
        if (!options.Enabled)
        {
            logger?.LogDebug("Skipped queueing RonFlow database Git pull refresh because sync is disabled. Reason: {Reason}", NormalizeReason(reason));
            return;
        }

        if (!IsPullRefreshDue())
        {
            return;
        }

        var normalizedReason = NormalizeReason(reason);
        int pendingCount;
        lock (pendingPullRequestsRoot)
        {
            if (!IsPullRefreshDue() || pendingPullReasons.Count > 0)
            {
                return;
            }

            pendingPullReasons.Enqueue(normalizedReason);
            pendingCount = pendingPullReasons.Count;
        }

        WriteDiagnosticLog($"Queued database Git pull refresh '{normalizedReason}'. PendingCount: {pendingCount}");
        logger?.LogInformation(
            "Queued RonFlow database Git pull refresh. Reason: {Reason}; PendingCount: {PendingCount}; LastUpdateTime: {LastUpdateTime:O}",
            normalizedReason,
            pendingCount,
            LastUpdateTime);
    }

    public bool FlushPendingPullRequests()
    {
        if (!options.Enabled)
        {
            return false;
        }

        var reasons = DrainPendingPullReasons();
        if (reasons.Count == 0)
        {
            return false;
        }

        if (!IsPullRefreshDue())
        {
            return false;
        }

        var reason = CreatePullRefreshReason(reasons);
        var completed = false;
        lock (syncRoot)
        {
            if (!IsPullRefreshDue())
            {
                return false;
            }

            completed = TryRun(
                $"pull database snapshot for queued refresh '{reason}'",
                () => PullAndMergeDatabaseSnapshot(reason));
        }

        if (completed)
        {
            MarkLastUpdateTime();
        }

        return completed;
    }

    private IReadOnlyList<string> DrainPendingPullReasons()
    {
        lock (pendingPullRequestsRoot)
        {
            if (pendingPullReasons.Count == 0)
            {
                return [];
            }

            var reasons = pendingPullReasons.ToArray();
            pendingPullReasons.Clear();
            return reasons;
        }
    }

    public void PushAfterMutation(string reason)
    {
        PushAfterMutation(reason, null);
    }

    public void PushAfterMutation(string reason, DatabaseSyncInitiator? initiator)
    {
        if (!options.Enabled)
        {
            logger?.LogDebug("Skipped queueing RonFlow database Git sync mutation because sync is disabled. Reason: {Reason}", NormalizeReason(reason));
            return;
        }

        var normalizedReason = NormalizeReason(reason);
        var operation = initiator is null
            ? null
            : operationStore.Create(initiator.UserId, normalizedReason, timeProvider.GetUtcNow());

        int pendingCount;
        lock (pendingMutationReasonsRoot)
        {
            pendingMutationRequests.Enqueue(new DatabaseSyncMutationRequest(normalizedReason, operation?.Id));
            pendingCount = pendingMutationRequests.Count;
        }

        WriteDiagnosticLog($"Queued database sync mutation '{normalizedReason}'. PendingCount: {pendingCount}");
        logger?.LogInformation(
            "Queued RonFlow database Git sync mutation. Reason: {Reason}; PendingCount: {PendingCount}",
            normalizedReason,
            pendingCount);
    }

    public bool FlushPendingMutations()
    {
        if (!options.Enabled)
        {
            return false;
        }

        var requests = DrainPendingMutationRequests();
        if (requests.Count == 0)
        {
            return false;
        }

        var operationIds = requests
            .Select(request => request.OperationId)
            .Where(operationId => operationId.HasValue)
            .Select(operationId => operationId!.Value)
            .Distinct()
            .ToArray();
        if (operationIds.Length > 0)
        {
            operationStore.MarkRunning(operationIds, timeProvider.GetUtcNow());
        }

        var reasons = requests.Select(request => request.Reason).ToArray();
        var reason = CreateCoalescedReason(reasons);
        var completedPullAndMerge = false;
        var runSucceeded = false;
        lock (syncRoot)
        {
            runSucceeded = TryRun(
                $"push database snapshot after coalesced mutations '{reason}'",
                () => completedPullAndMerge = PushDatabaseSnapshot(reason, reasons.Length));

            if (runSucceeded)
            {
                if (completedPullAndMerge)
                {
                    MarkLastUpdateTime();
                }
            }
        }

        var succeeded = runSucceeded && completedPullAndMerge;
        if (operationIds.Length > 0)
        {
            var completedOperations = operationStore.MarkCompleted(
                operationIds,
                succeeded,
                timeProvider.GetUtcNow(),
                succeeded ? null : UserVisibleFailureSummary);

            foreach (var operation in completedOperations)
            {
                notificationPublisher.Publish(new DatabaseSyncNotification(operation));
            }
        }

        return true;
    }

    private IReadOnlyList<DatabaseSyncMutationRequest> DrainPendingMutationRequests()
    {
        lock (pendingMutationReasonsRoot)
        {
            if (pendingMutationRequests.Count == 0)
            {
                return [];
            }

            var requests = pendingMutationRequests.ToArray();
            pendingMutationRequests.Clear();
            return requests;
        }
    }

    private void PullAndMergeDatabaseSnapshot(string reason)
    {
        repositorySync.EnsureReady();
        repositorySync.Pull();

        string? localSnapshotPath = null;
        string? mergedSnapshotPath = null;
        var shouldPushRepositorySnapshot = false;
        try
        {
            using (runtimeDatabaseAccessGate.EnterExclusive())
            {
                localSnapshotPath = TryCreateRuntimeSnapshot();
                var repositoryDatabasePath = GetRepositoryDatabasePath();
                if (localSnapshotPath is not null && File.Exists(repositoryDatabasePath))
                {
                    mergedSnapshotPath = CreateTemporarySnapshotPath("merged");
                    var mergeResult = snapshotMerger.Merge(localSnapshotPath, repositoryDatabasePath, mergedSnapshotPath);
                    if (!mergeResult.Succeeded)
                    {
                        throw new InvalidOperationException($"Database snapshot merge failed: {mergeResult.Message}");
                    }

                    snapshotStore.RestoreSnapshot(mergedSnapshotPath, repositoryDatabasePath);
                    snapshotStore.RestoreSnapshot(mergedSnapshotPath, options.RuntimeDatabasePath);
                    shouldPushRepositorySnapshot = true;
                }
                else if (localSnapshotPath is not null)
                {
                    snapshotStore.RestoreSnapshot(localSnapshotPath, repositoryDatabasePath);
                    shouldPushRepositorySnapshot = true;
                }
                else if (File.Exists(repositoryDatabasePath))
                {
                    snapshotStore.RestoreSnapshot(repositoryDatabasePath, options.RuntimeDatabasePath);
                }
            }

            if (shouldPushRepositorySnapshot)
            {
                repositorySync.Commit(options.DatabaseFileName, CreateCommitMessage(reason));
                repositorySync.Push();
            }
        }
        finally
        {
            if (localSnapshotPath is not null)
            {
                DeleteTemporarySnapshot(localSnapshotPath);
            }

            if (mergedSnapshotPath is not null)
            {
                DeleteTemporarySnapshot(mergedSnapshotPath);
            }
        }
    }

    private bool PushDatabaseSnapshot(string reason, int mutationCount)
    {
        WriteDiagnosticLog($"Flushing {mutationCount} queued database sync mutation(s). CoalescedReason: {reason}");
        logger?.LogInformation(
            "Flushing queued RonFlow database Git sync mutations. MutationCount: {MutationCount}; CoalescedReason: {CoalescedReason}",
            mutationCount,
            reason);

        repositorySync.EnsureReady();
        var localSnapshotPath = TryCreateRuntimeSnapshot();
        if (localSnapshotPath is null)
        {
            WriteDiagnosticLog("Skipped database sync flush because runtime database snapshot does not exist.");
            logger?.LogWarning(
                "Skipped RonFlow database Git sync flush because runtime database snapshot does not exist. RuntimeDatabasePath: {RuntimeDatabasePath}",
                options.RuntimeDatabasePath);
            return false;
        }

        repositorySync.Pull();

        var repositoryDatabasePath = GetRepositoryDatabasePath();
        if (File.Exists(repositoryDatabasePath))
        {
            var mergedSnapshotPath = CreateTemporarySnapshotPath("merged");
            try
            {
                var mergeResult = snapshotMerger.Merge(localSnapshotPath, repositoryDatabasePath, mergedSnapshotPath);
                if (!mergeResult.Succeeded)
                {
                    throw new InvalidOperationException($"Database snapshot merge failed: {mergeResult.Message}");
                }

                snapshotStore.RestoreSnapshot(mergedSnapshotPath, repositoryDatabasePath);
                repositorySync.Commit(options.DatabaseFileName, CreateCommitMessage(reason));
                repositorySync.Push();
                return true;
            }
            finally
            {
                DeleteTemporarySnapshot(localSnapshotPath);
                DeleteTemporarySnapshot(mergedSnapshotPath);
            }
        }

        try
        {
            snapshotStore.RestoreSnapshot(localSnapshotPath, repositoryDatabasePath);
            repositorySync.Commit(options.DatabaseFileName, CreateCommitMessage(reason));
            repositorySync.Push();
            return true;
        }
        finally
        {
            DeleteTemporarySnapshot(localSnapshotPath);
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

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "unspecified mutation"
            : reason.Trim();
    }

    private static string CreateCoalescedReason(IReadOnlyList<string> reasons)
    {
        var distinctReasons = reasons
            .Select(NormalizeReason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinctReasons.Length switch
        {
            0 => "coalesced mutations",
            1 => distinctReasons[0],
            _ => $"coalesced {reasons.Count} mutations: {string.Join(", ", distinctReasons)}",
        };
    }

    private static string CreatePullRefreshReason(IReadOnlyList<string> reasons)
    {
        var distinctReasons = reasons
            .Select(NormalizeReason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinctReasons.Length switch
        {
            0 => RequestPullReason,
            1 => distinctReasons[0],
            _ => $"{RequestPullReason}: {string.Join(", ", distinctReasons)}",
        };
    }

    private bool IsPullRefreshDue()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        lock (lastUpdateTimeRoot)
        {
            return lastUpdateTime == DateTime.MinValue || now - lastUpdateTime >= PullRefreshInterval;
        }
    }

    private void MarkLastUpdateTime()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        lock (lastUpdateTimeRoot)
        {
            lastUpdateTime = now;
        }

        WriteDiagnosticLog($"Updated database Git sync lastUpdateTime to {now:O}.");
        logger?.LogInformation("Updated RonFlow database Git sync lastUpdateTime. LastUpdateTime: {LastUpdateTime:O}", now);
    }

    private bool TryRun(string operation, Action action)
    {
        WriteDiagnosticLog($"Starting: {operation}");
        try
        {
            action();
            WriteDiagnosticLog($"Completed: {operation}");
            return true;
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
            return false;
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

    private sealed record DatabaseSyncMutationRequest(string Reason, Guid? OperationId);
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
