using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RonFlow.Diagnostics.Api;

public interface IBuildInfoReader
{
    Task<IReadOnlyList<BuildInfoResult>> ListAsync(CancellationToken cancellationToken);

    Task<BuildInfoResult?> ReadAsync(string sourceKey, CancellationToken cancellationToken);
}

public sealed class BuildInfoReader(IOptions<DiagnosticsOptions> options, ILogRedactor redactor) : IBuildInfoReader
{
    private readonly DiagnosticsOptions options = options.Value;

    public async Task<IReadOnlyList<BuildInfoResult>> ListAsync(CancellationToken cancellationToken)
    {
        var results = new List<BuildInfoResult>();
        foreach (var sourceKey in options.BuildInfoSources.Keys)
        {
            var result = await ReadAsync(sourceKey, cancellationToken);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    public async Task<BuildInfoResult?> ReadAsync(string sourceKey, CancellationToken cancellationToken)
    {
        if (!options.BuildInfoSources.TryGetValue(sourceKey, out var source))
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(source.DisplayName) ? sourceKey : source.DisplayName;
        if (string.IsNullOrWhiteSpace(source.Path) || !File.Exists(source.Path))
        {
            return new BuildInfoResult(sourceKey, displayName, false, null, null, null);
        }

        try
        {
            var info = new FileInfo(source.Path);
            await using var stream = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var parsed = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: cancellationToken);
            return new BuildInfoResult(sourceKey, displayName, true, info.LastWriteTimeUtc, parsed, null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new BuildInfoResult(sourceKey, displayName, true, null, null, redactor.Redact(exception.Message));
        }
    }
}

public sealed record BuildInfoResult(
    string Key,
    string DisplayName,
    bool Exists,
    DateTime? LastWriteTimeUtc,
    JsonElement? BuildInfo,
    string? Error);
