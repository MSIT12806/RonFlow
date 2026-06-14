using Microsoft.Extensions.Options;

namespace RonFlow.Diagnostics.Api.Tests;

public sealed class BuildInfoReaderTests
{
    [Test]
    public async Task ReadAsync_ReturnsParsedBuildInfoOrStructuredMissingResult()
    {
        using var workspace = new TemporaryWorkspace();
        var buildInfoPath = Path.Combine(workspace.Path, "build-info.json");
        await File.WriteAllTextAsync(buildInfoPath, """{"application":"RonFlow.Api","version":"20260614.000000"}""");
        var reader = new Api.BuildInfoReader(Options.Create(new Api.DiagnosticsOptions
        {
            BuildInfoSources = new(StringComparer.OrdinalIgnoreCase)
            {
                ["present"] = new Api.BuildInfoSourceOptions
                {
                    Path = buildInfoPath,
                    DisplayName = "Present",
                },
                ["missing"] = new Api.BuildInfoSourceOptions
                {
                    Path = Path.Combine(workspace.Path, "missing.json"),
                    DisplayName = "Missing",
                },
            },
        }), new Api.LogRedactor());

        var present = await reader.ReadAsync("present", CancellationToken.None);
        var missing = await reader.ReadAsync("missing", CancellationToken.None);

        Assert.That(present, Is.Not.Null);
        Assert.That(present!.Exists, Is.True);
        Assert.That(present.BuildInfo?.GetProperty("application").GetString(), Is.EqualTo("RonFlow.Api"));
        Assert.That(missing, Is.Not.Null);
        Assert.That(missing!.Exists, Is.False);
    }
}
