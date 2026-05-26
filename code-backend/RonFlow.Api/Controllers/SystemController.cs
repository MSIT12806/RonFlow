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
    [HttpGet("build-info")]
    [ProducesResponseType<BuildInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<BuildInfoResponse> GetBuildInfo([FromServices] IWebHostEnvironment environment)
    {
        return Ok(CreateBuildInfo(environment));
    }

    private static BuildInfoResponse CreateBuildInfo(IWebHostEnvironment environment)
    {
        var assembly = typeof(Program).Assembly;
        var assemblyVersion = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assemblyVersion;
        var deploymentMetadata = TryReadDeploymentMetadata(environment.ContentRootPath);
        var updatedAtUtc = deploymentMetadata?.UpdatedAtUtc ?? GetAssemblyUpdatedAtUtc(assembly);

        return new BuildInfoResponse(
            Application: deploymentMetadata?.Application ?? assembly.GetName().Name ?? "RonFlow.Api",
            EnvironmentName: environment.EnvironmentName,
            Version: deploymentMetadata?.Version ?? assemblyVersion,
            InformationalVersion: deploymentMetadata?.InformationalVersion ?? informationalVersion,
            UpdatedAtUtc: updatedAtUtc,
            SourceRevision: deploymentMetadata?.SourceRevision ?? TryExtractSourceRevision(informationalVersion));
    }

    private static DeploymentMetadata? TryReadDeploymentMetadata(string contentRootPath)
    {
        var metadataPath = Path.Combine(contentRootPath, "build-info.json");
        if (!System.IO.File.Exists(metadataPath))
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