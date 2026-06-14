using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RonFlow.Diagnostics.Api;

public interface ILogSourceReader
{
    IReadOnlyList<LogSourceSummary> ListSources();

    Task<LogTailResult?> ReadTailAsync(string sourceKey, int requestedTail, CancellationToken cancellationToken);
}

public interface ILogSourceProvider
{
    bool CanRead(LogSourceOptions source);

    LogSourceSummary GetSummary(string key, LogSourceOptions source);

    Task<LogTailResult> ReadTailAsync(
        string key,
        LogSourceOptions source,
        int tail,
        LogSourceSummary summary,
        CancellationToken cancellationToken);
}

public sealed class LogSourceReader(
    IOptions<DiagnosticsOptions> options,
    IEnumerable<ILogSourceProvider> providers,
    ILogRedactor redactor) : ILogSourceReader
{
    private readonly DiagnosticsOptions options = options.Value;
    private readonly IReadOnlyList<ILogSourceProvider> providers = providers.ToArray();

    public IReadOnlyList<LogSourceSummary> ListSources()
    {
        return this.options.LogSources
            .Select(source => GetProvider(source.Value).GetSummary(source.Key, source.Value))
            .ToArray();
    }

    public async Task<LogTailResult?> ReadTailAsync(string sourceKey, int requestedTail, CancellationToken cancellationToken)
    {
        if (!options.LogSources.TryGetValue(sourceKey, out var source))
        {
            return null;
        }

        var provider = GetProvider(source);
        var summary = provider.GetSummary(sourceKey, source);
        var providerMaxTail = source.Provider is LogSourceProviderKind.File
            ? null
            : source.Centralized.MaxTailLines;
        var maxTail = providerMaxTail.GetValueOrDefault(options.MaxTailLines);
        var tail = Math.Clamp(requestedTail, 1, Math.Max(1, maxTail));
        return await provider.ReadTailAsync(sourceKey, source, tail, summary, cancellationToken);
    }

    private ILogSourceProvider GetProvider(LogSourceOptions source)
    {
        var provider = providers.FirstOrDefault(candidate => candidate.CanRead(source));
        if (provider is not null)
        {
            return provider;
        }

        return new UnsupportedLogSourceProvider(redactor);
    }
}

public sealed class FileLogSourceProvider(ILogRedactor redactor) : ILogSourceProvider
{
    public bool CanRead(LogSourceOptions source) => source.Provider is LogSourceProviderKind.File;

    public LogSourceSummary GetSummary(string key, LogSourceOptions source)
    {
        var displayName = GetDisplayName(key, source);
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
                return new LogSourceSummary(
                    key,
                    displayName,
                    source.Provider.ToString(),
                    false,
                    false,
                    0,
                    null,
                    null,
                    null,
                    null);
            }

            return new LogSourceSummary(
                key,
                displayName,
                source.Provider.ToString(),
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
            return new LogSourceSummary(
                key,
                displayName,
                source.Provider.ToString(),
                false,
                false,
                0,
                null,
                null,
                null,
                redactor.Redact(exception.Message));
        }
    }

    public async Task<LogTailResult> ReadTailAsync(
        string key,
        LogSourceOptions source,
        int tail,
        LogSourceSummary summary,
        CancellationToken cancellationToken)
    {
        if (!summary.Exists || !summary.Readable || summary.LatestFilePath is null)
        {
            return new LogTailResult(
                key,
                summary.DisplayName,
                source.Provider.ToString(),
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
                key,
                summary.DisplayName,
                source.Provider.ToString(),
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
                key,
                summary.DisplayName,
                source.Provider.ToString(),
                true,
                false,
                summary.LatestFileName,
                summary.LatestLastWriteTimeUtc,
                tail,
                [],
                redactor.Redact(exception.Message));
        }
    }

    private static async Task<IReadOnlyList<string>> ReadLastLinesAsync(
        string path,
        int tail,
        CancellationToken cancellationToken)
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

    private static string GetDisplayName(string key, LogSourceOptions source) =>
        string.IsNullOrWhiteSpace(source.DisplayName) ? key : source.DisplayName;
}

public sealed class CentralizedLogSourceProvider(HttpClient httpClient, ILogRedactor redactor) : ILogSourceProvider
{
    public bool CanRead(LogSourceOptions source) =>
        source.Provider is LogSourceProviderKind.Elasticsearch or LogSourceProviderKind.OpenSearch;

    public LogSourceSummary GetSummary(string key, LogSourceOptions source)
    {
        var displayName = string.IsNullOrWhiteSpace(source.DisplayName) ? key : source.DisplayName;
        var error = Validate(source.Centralized);
        return new LogSourceSummary(
            key,
            displayName,
            source.Provider.ToString(),
            error is null,
            error is null,
            0,
            null,
            null,
            null,
            error);
    }

    public async Task<LogTailResult> ReadTailAsync(
        string key,
        LogSourceOptions source,
        int tail,
        LogSourceSummary summary,
        CancellationToken cancellationToken)
    {
        if (!summary.Readable)
        {
            return CreateFailure(key, summary.DisplayName, source.Provider, tail, summary.Error);
        }

        try
        {
            var requestUri = BuildSearchUri(source.Centralized);
            using var response = await httpClient.PostAsJsonAsync(
                requestUri,
                BuildSearchRequest(source.Centralized, tail),
                cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CreateFailure(
                    key,
                    summary.DisplayName,
                    source.Provider,
                    tail,
                    $"Centralized log provider returned {(int)response.StatusCode}: {responseText}");
            }

            var lines = ReadElasticLines(responseText)
                .Take(tail)
                .Select(redactor.Redact)
                .ToArray();
            return new LogTailResult(
                key,
                summary.DisplayName,
                source.Provider.ToString(),
                true,
                true,
                null,
                null,
                tail,
                lines,
                null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return CreateFailure(key, summary.DisplayName, source.Provider, tail, exception.Message);
        }
    }

    private LogTailResult CreateFailure(
        string key,
        string displayName,
        LogSourceProviderKind provider,
        int tail,
        string? error) =>
        new(
            key,
            displayName,
            provider.ToString(),
            true,
            false,
            null,
            null,
            tail,
            [],
            error is null ? null : redactor.Redact(error));

    private static string? Validate(CentralizedLogSourceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return "Centralized log endpoint is not configured.";
        }

        if (string.IsNullOrWhiteSpace(options.IndexPattern))
        {
            return "Centralized log index pattern is not configured.";
        }

        return null;
    }

    private static Uri BuildSearchUri(CentralizedLogSourceOptions source)
    {
        var endpoint = source.Endpoint.TrimEnd('/');
        var index = Uri.EscapeDataString(source.IndexPattern).Replace("%2A", "*", StringComparison.Ordinal);
        return new Uri($"{endpoint}/{index}/_search");
    }

    private static object BuildSearchRequest(CentralizedLogSourceOptions source, int tail)
    {
        var filters = new List<object>
        {
            new
            {
                range = new Dictionary<string, object>
                {
                    ["@timestamp"] = new
                    {
                        gte = $"now-{Math.Max(1, source.TimeRangeMinutes)}m",
                        lte = "now",
                    },
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(source.ServiceName))
        {
            filters.Add(new { term = new Dictionary<string, string> { ["service.name"] = source.ServiceName } });
        }

        if (!string.IsNullOrWhiteSpace(source.Environment))
        {
            filters.Add(new { term = new Dictionary<string, string> { ["service.environment"] = source.Environment } });
        }

        if (!string.IsNullOrWhiteSpace(source.QueryPattern))
        {
            filters.Add(new { query_string = new { query = source.QueryPattern } });
        }

        return new
        {
            size = tail,
            sort = new object[] { new Dictionary<string, string> { ["@timestamp"] = "desc" } },
            query = new
            {
                @bool = new
                {
                    filter = filters,
                },
            },
        };
    }

    private static IReadOnlyList<string> ReadElasticLines(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        if (!document.RootElement.TryGetProperty("hits", out var hits) ||
            !hits.TryGetProperty("hits", out var hitItems))
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var hit in hitItems.EnumerateArray())
        {
            if (!hit.TryGetProperty("_source", out var source))
            {
                continue;
            }

            var timestamp = ReadFirstString(source, "@timestamp", "timestamp", "time");
            var message = ReadFirstString(source, "message", "log", "renderedMessage");
            if (string.IsNullOrWhiteSpace(message))
            {
                message = source.ToString();
            }

            lines.Add(string.IsNullOrWhiteSpace(timestamp) ? message : $"{timestamp} {message}");
        }

        return lines;
    }

    private static string? ReadFirstString(JsonElement source, params string[] names)
    {
        foreach (var name in names)
        {
            if (source.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }
}

public sealed class UnsupportedLogSourceProvider(ILogRedactor redactor) : ILogSourceProvider
{
    public bool CanRead(LogSourceOptions source) => true;

    public LogSourceSummary GetSummary(string key, LogSourceOptions source)
    {
        var displayName = string.IsNullOrWhiteSpace(source.DisplayName) ? key : source.DisplayName;
        return new LogSourceSummary(
            key,
            displayName,
            source.Provider.ToString(),
            false,
            false,
            0,
            null,
            null,
            null,
            redactor.Redact($"Unsupported log provider: {source.Provider}"));
    }

    public Task<LogTailResult> ReadTailAsync(
        string key,
        LogSourceOptions source,
        int tail,
        LogSourceSummary summary,
        CancellationToken cancellationToken) =>
        Task.FromResult(new LogTailResult(
            key,
            summary.DisplayName,
            source.Provider.ToString(),
            false,
            false,
            null,
            null,
            tail,
            [],
            summary.Error));
}

public sealed record LogSourceSummary(
    string Key,
    string DisplayName,
    string Provider,
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
    string Provider,
    bool Exists,
    bool Readable,
    string? FileName,
    DateTime? LastWriteTimeUtc,
    int Tail,
    IReadOnlyList<string> Lines,
    string? Error);
