using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace RonFlow.Diagnostics.Api;

public interface IConfiguredHealthCheckRunner
{
    Task<IReadOnlyList<ConfiguredHealthCheckResult>> RunAllAsync(CancellationToken cancellationToken);

    Task<ConfiguredHealthCheckResult?> RunAsync(string sourceKey, CancellationToken cancellationToken);
}

public sealed class ConfiguredHealthCheckRunner(
    HttpClient httpClient,
    IOptions<DiagnosticsOptions> options,
    ILogRedactor redactor,
    TimeProvider timeProvider) : IConfiguredHealthCheckRunner
{
    private readonly DiagnosticsOptions options = options.Value;

    public async Task<IReadOnlyList<ConfiguredHealthCheckResult>> RunAllAsync(CancellationToken cancellationToken)
    {
        var results = new List<ConfiguredHealthCheckResult>();
        foreach (var sourceKey in options.HealthChecks.Keys)
        {
            var result = await RunAsync(sourceKey, cancellationToken);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    public async Task<ConfiguredHealthCheckResult?> RunAsync(string sourceKey, CancellationToken cancellationToken)
    {
        if (!options.HealthChecks.TryGetValue(sourceKey, out var source))
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(source.DisplayName) ? sourceKey : source.DisplayName;
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri))
        {
            return new ConfiguredHealthCheckResult(sourceKey, displayName, null, null, false, 0, timeProvider.GetUtcNow(), "Configured URL is invalid.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();
            var statusCode = (int)response.StatusCode;
            return new ConfiguredHealthCheckResult(
                sourceKey,
                displayName,
                uri.Host,
                uri.PathAndQuery,
                source.ExpectedStatusCodes.Contains(statusCode),
                statusCode,
                timeProvider.GetUtcNow(),
                null,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            stopwatch.Stop();
            return new ConfiguredHealthCheckResult(
                sourceKey,
                displayName,
                uri.Host,
                uri.PathAndQuery,
                false,
                null,
                timeProvider.GetUtcNow(),
                redactor.Redact(exception.Message),
                stopwatch.ElapsedMilliseconds);
        }
    }
}

public sealed record ConfiguredHealthCheckResult(
    string Key,
    string DisplayName,
    string? Host,
    string? Path,
    bool IsExpectedStatusCode,
    int? StatusCode,
    DateTimeOffset CheckedAtUtc,
    string? Error,
    long ElapsedMilliseconds = 0);
