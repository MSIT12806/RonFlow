using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace RonFlow.Diagnostics.Api;

public interface IGitRepositoryInspector
{
    Task<GitRepositoryStatus?> GetStatusAsync(string repoKey, CancellationToken cancellationToken);
}

public sealed class GitRepositoryInspector(
    IOptions<DiagnosticsOptions> options,
    ILogRedactor redactor,
    TimeProvider timeProvider) : IGitRepositoryInspector
{
    private const int MaxStatusLineCount = 200;
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(5);
    private readonly DiagnosticsOptions options = options.Value;

    public async Task<GitRepositoryStatus?> GetStatusAsync(string repoKey, CancellationToken cancellationToken)
    {
        if (!options.GitRepositories.TryGetValue(repoKey, out var repository))
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(repository.DisplayName) ? repoKey : repository.DisplayName;
        if (string.IsNullOrWhiteSpace(repository.Path) || !Directory.Exists(repository.Path))
        {
            return GitRepositoryStatus.Missing(repoKey, displayName, timeProvider.GetUtcNow(), "Repository path does not exist.");
        }

        if (!Directory.Exists(Path.Combine(repository.Path, ".git")))
        {
            return GitRepositoryStatus.NotRepository(repoKey, displayName, timeProvider.GetUtcNow(), "Configured path is not a Git repository.");
        }

        var branch = await RunGitAsync(repository.Path, ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken);
        var latestCommit = await RunGitAsync(repository.Path, ["log", "-1", "--format=%h%x00%s"], cancellationToken);
        var status = await RunGitAsync(repository.Path, ["status", "--porcelain"], cancellationToken);
        var remoteNames = await RunGitAsync(repository.Path, ["remote"], cancellationToken);

        var firstError = new[] { branch, latestCommit, status, remoteNames }
            .FirstOrDefault(result => !result.Succeeded)?.Error;
        var statusLines = status.Output
            .SplitLines()
            .Take(MaxStatusLineCount)
            .Select(redactor.Redact)
            .ToArray();
        var latestCommitParts = latestCommit.Output.Split('\0', 2);

        return new GitRepositoryStatus(
            repoKey,
            displayName,
            true,
            true,
            branch.Succeeded ? redactor.Redact(branch.Output.Trim()) : null,
            latestCommit.Succeeded && latestCommitParts.Length > 0 && !string.IsNullOrWhiteSpace(latestCommitParts[0])
                ? new GitCommitSummary(redactor.Redact(latestCommitParts[0].Trim()), latestCommitParts.Length > 1 ? redactor.Redact(latestCommitParts[1].Trim()) : string.Empty)
                : null,
            status.Succeeded && statusLines.Length == 0,
            statusLines,
            remoteNames.Output.SplitLines().Select(redactor.Redact).ToArray(),
            timeProvider.GetUtcNow(),
            firstError is null ? null : redactor.Redact(firstError));
    }

    private async Task<GitCommandResult> RunGitAsync(string repositoryPath, string[] arguments, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(GitTimeout);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(repositoryPath);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return GitCommandResult.Failed("Failed to start git process.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            var output = redactor.Redact(await outputTask);
            var error = redactor.Redact(await errorTask);
            return process.ExitCode == 0
                ? GitCommandResult.Success(output)
                : GitCommandResult.Failed(string.IsNullOrWhiteSpace(error) ? $"git exited with code {process.ExitCode}" : error);
        }
        catch (OperationCanceledException)
        {
            return GitCommandResult.Failed("git command timed out.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            return GitCommandResult.Failed(redactor.Redact(exception.Message));
        }
    }
}

public sealed record GitRepositoryStatus(
    string Key,
    string DisplayName,
    bool Exists,
    bool IsRepository,
    string? Branch,
    GitCommitSummary? LatestCommit,
    bool WorkingTreeClean,
    IReadOnlyList<string> StatusLines,
    IReadOnlyList<string> RemoteNames,
    DateTimeOffset CheckedAtUtc,
    string? Error)
{
    public static GitRepositoryStatus Missing(string key, string displayName, DateTimeOffset checkedAtUtc, string error) =>
        new(key, displayName, false, false, null, null, false, [], [], checkedAtUtc, error);

    public static GitRepositoryStatus NotRepository(string key, string displayName, DateTimeOffset checkedAtUtc, string error) =>
        new(key, displayName, true, false, null, null, false, [], [], checkedAtUtc, error);
}

public sealed record GitCommitSummary(string Sha, string Subject);

internal sealed record GitCommandResult(bool Succeeded, string Output, string? Error)
{
    public static GitCommandResult Success(string output) => new(true, output, null);

    public static GitCommandResult Failed(string error) => new(false, string.Empty, error);
}

internal static class StringLineExtensions
{
    public static string[] SplitLines(this string value) =>
        value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
