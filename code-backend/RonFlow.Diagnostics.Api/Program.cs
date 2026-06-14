using Microsoft.Extensions.Options;
using RonFlow.Diagnostics.Api;

namespace RonFlow.Diagnostics.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<DiagnosticsOptions>(builder.Configuration.GetSection(DiagnosticsOptions.SectionName));
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<ILogRedactor, LogRedactor>();
        builder.Services.AddSingleton<ILogSourceProvider, FileLogSourceProvider>();
        builder.Services.AddHttpClient<CentralizedLogSourceProvider>();
        builder.Services.AddSingleton<ILogSourceProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<CentralizedLogSourceProvider>());
        builder.Services.AddSingleton<ILogSourceReader, LogSourceReader>();
        builder.Services.AddSingleton<IGitRepositoryInspector, GitRepositoryInspector>();
        builder.Services.AddSingleton<IBuildInfoReader, BuildInfoReader>();
        builder.Services.AddHttpClient<IConfiguredHealthCheckRunner, ConfiguredHealthCheckRunner>();

        var app = builder.Build();

        app.MapGet("/api/health", (TimeProvider timeProvider) => Results.Ok(new
        {
            status = "healthy",
            application = "RonFlow.Diagnostics.Api",
            checkedAtUtc = timeProvider.GetUtcNow(),
        }));

        app.MapGet("/api/sources", (IOptions<DiagnosticsOptions> options) =>
        {
            var diagnostics = options.Value;
            return Results.Ok(new
            {
                logSources = diagnostics.LogSources.Select(SourceInventoryItem.Create),
                gitRepositories = diagnostics.GitRepositories.Select(SourceInventoryItem.Create),
                buildInfoSources = diagnostics.BuildInfoSources.Select(SourceInventoryItem.Create),
                healthChecks = diagnostics.HealthChecks.Select(SourceInventoryItem.Create),
            });
        });

        app.MapGet("/api/logs", (ILogSourceReader reader) => Results.Ok(reader.ListSources()));

        app.MapGet("/api/logs/{sourceKey}", async (string sourceKey, int? tail, ILogSourceReader reader) =>
        {
            var result = await reader.ReadTailAsync(sourceKey, tail ?? 200, CancellationToken.None);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapGet("/api/git-repositories/{repoKey}/status", async (string repoKey, IGitRepositoryInspector inspector) =>
        {
            var result = await inspector.GetStatusAsync(repoKey, CancellationToken.None);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapGet("/api/build-info", async (IBuildInfoReader reader) => Results.Ok(await reader.ListAsync(CancellationToken.None)));

        app.MapGet("/api/build-info/{sourceKey}", async (string sourceKey, IBuildInfoReader reader) =>
        {
            var result = await reader.ReadAsync(sourceKey, CancellationToken.None);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapGet("/api/health-checks", async (IConfiguredHealthCheckRunner runner) =>
            Results.Ok(await runner.RunAllAsync(CancellationToken.None)));

        app.MapGet("/api/health-checks/{sourceKey}", async (string sourceKey, IConfiguredHealthCheckRunner runner) =>
        {
            var result = await runner.RunAsync(sourceKey, CancellationToken.None);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.Run();
    }
}
