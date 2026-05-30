using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/system")]
[AllowAnonymous]
public sealed class SystemController : ControllerBase
{
    private const string FrontendBuildInfoPathVariable = "RONFLOW_DEPLOYMENT_SUMMARY_FRONTEND_BUILD_INFO_PATH";
    private const string RonFlowApiBuildInfoPathVariable = "RONFLOW_DEPLOYMENT_SUMMARY_RONFLOW_API_BUILD_INFO_PATH";
    private const string RonAuthApiBuildInfoPathVariable = "RONFLOW_DEPLOYMENT_SUMMARY_RONAUTH_API_BUILD_INFO_PATH";

    [HttpGet("build-info")]
    [ProducesResponseType<DeploymentSummaryResponse>(StatusCodes.Status200OK)]
    public ActionResult<DeploymentSummaryResponse> GetBuildInfo([FromServices] IWebHostEnvironment environment)
    {
        return Ok(CreateDeploymentSummary(environment, Request.Host.Host));
    }

    private static DeploymentSummaryResponse CreateDeploymentSummary(IWebHostEnvironment environment, string? requestHost)
    {
        var frontend = CreateMetadataBackedComponent(
            ResolveBuildInfoPath(FrontendBuildInfoPathVariable, environment.ContentRootPath, "ronflow-web"),
            "RonFlow.Web");

        var ronFlowApi = CreateRonFlowApiComponent(environment);

        var ronAuthApi = CreateMetadataBackedComponent(
            ResolveBuildInfoPath(RonAuthApiBuildInfoPathVariable, environment.ContentRootPath, "ronauth-api"),
            "RonAuth.Api");

        return new DeploymentSummaryResponse(
            Environment: string.IsNullOrWhiteSpace(requestHost) ? environment.EnvironmentName : requestHost,
            Frontend: frontend,
            RonFlowApi: ronFlowApi,
            RonAuthApi: ronAuthApi,
            IsSameDeployment: IsSameDeployment(frontend, ronFlowApi, ronAuthApi));
    }

    private static DeploymentComponentResponse CreateRonFlowApiComponent(IWebHostEnvironment environment)
    {
        var assembly = typeof(Program).Assembly;
        var assemblyVersion = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assemblyVersion;
        var fallback = new DeploymentComponentResponse(
            Application: assembly.GetName().Name ?? "RonFlow.Api",
            Version: assemblyVersion,
            InformationalVersion: informationalVersion,
            UpdatedAtUtc: GetAssemblyUpdatedAtUtc(assembly),
            SourceRevision: TryExtractSourceRevision(informationalVersion));

        var configuredMetadataPath = ResolveBuildInfoPath(RonFlowApiBuildInfoPathVariable, environment.ContentRootPath, "ronflow-api");
        var deploymentMetadata = TryReadDeploymentMetadata(configuredMetadataPath)
            ?? TryReadDeploymentMetadata(Path.Combine(environment.ContentRootPath, "build-info.json"));

        return deploymentMetadata is null
            ? fallback
            : CreateComponentResponse(deploymentMetadata, fallback.Application, fallback.UpdatedAtUtc);
    }

    private static DeploymentComponentResponse CreateMetadataBackedComponent(string metadataPath, string applicationName)
    {
        var deploymentMetadata = TryReadDeploymentMetadata(metadataPath);
        return deploymentMetadata is null
            ? CreateUnavailableComponent(applicationName)
            : CreateComponentResponse(deploymentMetadata, applicationName, DateTimeOffset.UtcNow);
    }

    private static DeploymentComponentResponse CreateComponentResponse(
        DeploymentMetadata deploymentMetadata,
        string defaultApplicationName,
        DateTimeOffset fallbackUpdatedAtUtc)
    {
        var version = string.IsNullOrWhiteSpace(deploymentMetadata.Version) ? "unknown" : deploymentMetadata.Version;
        var informationalVersion = string.IsNullOrWhiteSpace(deploymentMetadata.InformationalVersion)
            ? version
            : deploymentMetadata.InformationalVersion;

        return new DeploymentComponentResponse(
            Application: string.IsNullOrWhiteSpace(deploymentMetadata.Application) ? defaultApplicationName : deploymentMetadata.Application,
            Version: version,
            InformationalVersion: informationalVersion,
            UpdatedAtUtc: deploymentMetadata.UpdatedAtUtc ?? fallbackUpdatedAtUtc,
            SourceRevision: deploymentMetadata.SourceRevision ?? TryExtractSourceRevision(informationalVersion));
    }

    private static DeploymentComponentResponse CreateUnavailableComponent(string applicationName)
    {
        return new DeploymentComponentResponse(
            Application: applicationName,
            Version: "unknown",
            InformationalVersion: "unknown",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SourceRevision: null);
    }

    private static bool IsSameDeployment(params DeploymentComponentResponse[] components)
    {
        return components.Length > 0
            && components.All(component => !string.Equals(component.Version, "unknown", StringComparison.OrdinalIgnoreCase))
            && components.Select(component => component.Version).Distinct(StringComparer.Ordinal).Count() == 1
            && components.Select(component => component.UpdatedAtUtc.ToUniversalTime()).Distinct().Count() == 1;
    }

    private static string ResolveBuildInfoPath(string environmentVariableName, string contentRootPath, string siblingDirectoryName)
    {
        var configuredPath = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? configuredPath
                : Path.Combine(configuredPath, "build-info.json");
        }

        var deploymentRootPath = Directory.GetParent(contentRootPath)?.FullName;
        return string.IsNullOrWhiteSpace(deploymentRootPath)
            ? Path.Combine(contentRootPath, "build-info.json")
            : Path.Combine(deploymentRootPath, siblingDirectoryName, "build-info.json");
    }

    private static DeploymentMetadata? TryReadDeploymentMetadata(string? metadataPath)
    {
        if (string.IsNullOrWhiteSpace(metadataPath) || !System.IO.File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = System.IO.File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<DeploymentMetadata>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset GetAssemblyUpdatedAtUtc(Assembly assembly)
    {
        var assemblyLocation = assembly.Location;
        if (string.IsNullOrWhiteSpace(assemblyLocation) || !System.IO.File.Exists(assemblyLocation))
        {
            return DateTimeOffset.UtcNow;
        }

        return new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(assemblyLocation), TimeSpan.Zero);
    }

    private static string? TryExtractSourceRevision(string informationalVersion)
    {
        var separatorIndex = informationalVersion.LastIndexOf('+');
        if (separatorIndex < 0 || separatorIndex == informationalVersion.Length - 1)
        {
            return null;
        }

        return informationalVersion[(separatorIndex + 1)..];
    }

    private sealed record DeploymentMetadata(
        string? Application,
        string? Version,
        string? InformationalVersion,
        DateTimeOffset? UpdatedAtUtc,
        string? SourceRevision);
}