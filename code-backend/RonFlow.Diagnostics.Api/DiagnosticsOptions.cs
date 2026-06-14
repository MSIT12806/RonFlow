namespace RonFlow.Diagnostics.Api;

public sealed class DiagnosticsOptions
{
    public const string SectionName = "Diagnostics";

    public Dictionary<string, LogSourceOptions> LogSources { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, GitRepositoryOptions> GitRepositories { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, BuildInfoSourceOptions> BuildInfoSources { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HealthCheckSourceOptions> HealthChecks { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int MaxTailLines { get; init; } = 1000;
}

public sealed class LogSourceOptions
{
    public string PathPattern { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class GitRepositoryOptions
{
    public string Path { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class BuildInfoSourceOptions
{
    public string Path { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class HealthCheckSourceOptions
{
    public string Url { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int[] ExpectedStatusCodes { get; init; } = [200];
}

public sealed record SourceInventoryItem(string Key, string DisplayName)
{
    public static SourceInventoryItem Create<TOptions>(KeyValuePair<string, TOptions> source)
    {
        var displayName = source.Value switch
        {
            LogSourceOptions logSource => logSource.DisplayName,
            GitRepositoryOptions repository => repository.DisplayName,
            BuildInfoSourceOptions buildInfo => buildInfo.DisplayName,
            HealthCheckSourceOptions healthCheck => healthCheck.DisplayName,
            _ => source.Key,
        };

        return new SourceInventoryItem(source.Key, string.IsNullOrWhiteSpace(displayName) ? source.Key : displayName);
    }
}
