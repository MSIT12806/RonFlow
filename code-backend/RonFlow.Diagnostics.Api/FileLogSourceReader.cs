using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace RonFlow.Diagnostics.Api;

public interface ILogSourceReader
{
    IReadOnlyList<LogSourceSummary> ListSources();

    Task<LogTailResult?> ReadTailAsync(string sourceKey, int requestedTail, CancellationToken cancellationToken);
}

public sealed class FileLogSourceReader(IOptions<DiagnosticsOptions> options, ILogRedactor redactor) : ILogSourceReader
{
    private readonly DiagnosticsOptions options = options.Value;

    public IReadOnlyList<LogSourceSummary> ListSources()
    {
        return this.options.LogSources
            .Select(source => GetSummary(source.Key, source.Value))
            .ToArray();
    }

    public async Task<LogTailResult?> ReadTailAsync(string sourceKey, int requestedTail, CancellationToken cancellationToken)
    {
        if (!options.LogSources.TryGetValue(sourceKey, out var source))
        {
            return null;
        }

        var summary = GetSummary(sourceKey, source);
        var tail = Math.Clamp(requestedTail, 1, Math.Max(1, options.MaxTailLines));
        if (!summary.Exists || !summary.Readable || summary.LatestFilePath is null)
        {
            return new LogTailResult(
                sourceKey,
                summary.DisplayName,
                summary.Exists,
                summary.Readable,
                summary.LatestFileName,
                summary.LatestLastWriteTimeUtc,
                tail,
                [],
                summary.Error);
        }

        try
        {
            var lines = await ReadLastLinesAsync(summary.LatestFilePath, tail, cancellationToken);
            return new LogTailResult(
                sourceKey,
                summary.DisplayName,
                true,
                true,
                summary.LatestFileName,
                summary.LatestLastWriteTimeUtc,
                tail,
                lines.Select(redactor.Redact).ToArray(),
                null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new LogTailResult(
                sourceKey,
                summary.DisplayName,
                true,
                false,
                summary.LatestFileName,
                summary.LatestLastWriteTimeUtc,
                tail,
                [],
                redactor.Redact(exception.Message));
        }
    }

    private static async Task<IReadOnlyList<string>> ReadLastLinesAsync(string path, int tail, CancellationToken cancellationToken)
    {
        var queue = new Queue<string>(tail);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (queue.Count == tail)
            {
                queue.Dequeue();
            }

            queue.Enqueue(line);
        }

        return queue.ToArray();
    }

    private LogSourceSummary GetSummary(string key, LogSourceOptions source)
    {
        var displayName = string.IsNullOrWhiteSpace(source.DisplayName) ? key : source.DisplayName;
        try
        {
            var files = ResolveFiles(source.PathPattern)
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();
            var latest = files.FirstOrDefault();

            if (latest is null)
            {
                return new LogSourceSummary(key, displayName, false, false, 0, null, null, null, null);
            }

            return new LogSourceSummary(
                key,
                displayName,
                true,
                CanRead(latest.FullName, out var error),
                files.Length,
                latest.Name,
                latest.LastWriteTimeUtc,
                latest.FullName,
                error is null ? null : redactor.Redact(error));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new LogSourceSummary(key, displayName, false, false, 0, null, null, null, redactor.Redact(exception.Message));
        }
    }

    private static IEnumerable<string> ResolveFiles(string pathPattern)
    {
        if (string.IsNullOrWhiteSpace(pathPattern))
        {
            return [];
        }

        var directory = Path.GetDirectoryName(pathPattern);
        var pattern = Path.GetFileName(pathPattern);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "*";
        }

        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly);
    }

    private static bool CanRead(string path, out string? error)
    {
        try
        {
            using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }
}

public sealed record LogSourceSummary(
    string Key,
    string DisplayName,
    bool Exists,
    bool Readable,
    int MatchedFileCount,
    string? LatestFileName,
    DateTime? LatestLastWriteTimeUtc,
    [property: JsonIgnore]
    string? LatestFilePath,
    string? Error);

public sealed record LogTailResult(
    string Key,
    string DisplayName,
    bool Exists,
    bool Readable,
    string? FileName,
    DateTime? LastWriteTimeUtc,
    int Tail,
    IReadOnlyList<string> Lines,
    string? Error);
