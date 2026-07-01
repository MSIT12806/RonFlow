using System.Text.Json;

namespace RonFlow.Diagnostics.Api.Tests;

public sealed class CentralizedLoggingSandboxFilesTests
{
    [Test]
    public void SandboxCompose_DefinesOpenSearchDashboardsAndFluentBit()
    {
        var sandboxRoot = FindSandboxRoot();
        var compose = File.ReadAllText(Path.Combine(sandboxRoot, "docker-compose.yml"));

        Assert.That(compose, Does.Contain("opensearchproject/opensearch:"));
        Assert.That(compose, Does.Contain("opensearchproject/opensearch-dashboards:"));
        Assert.That(compose, Does.Contain("cr.fluentbit.io/fluent/fluent-bit:"));
        Assert.That(compose, Does.Contain("9200:9200"));
        Assert.That(compose, Does.Contain("5601:5601"));
        Assert.That(compose, Does.Contain("C:/inetpub/ronflow-api/logs"));
        Assert.That(compose, Does.Contain("C:/inetpub/ronauth-api/logs"));
        Assert.That(compose, Does.Contain("C:/inetpub/ronflow-api/App_Data"));
    }

    [Test]
    public void FluentBitConfig_DefinesServiceFieldsAndRedactionFilter()
    {
        var sandboxRoot = FindSandboxRoot();
        var config = File.ReadAllText(Path.Combine(sandboxRoot, "fluent-bit", "fluent-bit.conf"));
        var redaction = File.ReadAllText(Path.Combine(sandboxRoot, "fluent-bit", "redact.lua"));

        Assert.That(config, Does.Contain("service.name ronflow-api"));
        Assert.That(config, Does.Contain("service.name ronauth-api"));
        Assert.That(config, Does.Contain("service.name database-git-sync"));
        Assert.That(config, Does.Contain("service.environment localhost"));
        Assert.That(config, Does.Contain("database-git-sync.log"));
        Assert.That(config, Does.Contain("Name    lua"));
        Assert.That(config, Does.Contain("Index                 ronflow-logs"));
        Assert.That(redaction, Does.Contain("github_pat_***"));
        Assert.That(redaction, Does.Contain("Bearer ***"));
        Assert.That(redaction, Does.Contain("Password="));
    }

    [Test]
    public void DiagnosticsExample_ContainsExecutableOpenSearchLogSources()
    {
        var sandboxRoot = FindSandboxRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            sandboxRoot,
            "diagnostics.appsettings.example.json")));
        var logSources = document.RootElement
            .GetProperty("Diagnostics")
            .GetProperty("LogSources");

        Assert.That(logSources.TryGetProperty("ronflow-centralized-sample", out var sample), Is.True);
        Assert.That(sample.GetProperty("Provider").GetString(), Is.EqualTo("OpenSearch"));
        Assert.That(sample.GetProperty("Centralized").GetProperty("Endpoint").GetString(), Is.EqualTo("http://localhost:9200"));
        Assert.That(sample.GetProperty("Centralized").GetProperty("IndexPattern").GetString(), Is.EqualTo("ronflow-logs-*"));
        Assert.That(sample.GetProperty("Centralized").GetProperty("ServiceName").GetString(), Is.EqualTo("ronflow-sample"));
    }

    private static string FindSandboxRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "centralized-logging");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find scripts/centralized-logging from the test directory.");
    }
}
