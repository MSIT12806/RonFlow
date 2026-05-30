using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RonFlow.Api.Tests;

public sealed class SystemBuildInfoApiIntegrationTests : ApiIntegrationTestBase
{
    [Test]
    public async Task GetBuildInfo_WhenAnonymous_ReturnsDeploymentSummaryAcrossFrontendAndApis()
    {
        using var anonymousClient = CreateAnonymousClient();
        using var fixture = DeploymentSummaryFixture.Create();

        fixture.Apply();

        try
        {
            var response = await anonymousClient.GetAsync("/api/system/build-info");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            await using var payloadStream = await response.Content.ReadAsStreamAsync();
            using var payload = await JsonDocument.ParseAsync(payloadStream);
            var root = payload.RootElement;

            Assert.Multiple(() =>
            {
                Assert.That(root.GetProperty("environment").GetString(), Is.EqualTo("localhost"));
                Assert.That(root.GetProperty("isSameDeployment").GetBoolean(), Is.True);

                AssertComponent(root.GetProperty("frontend"), "RonFlow.Web", "20260530.101500", "ronflow-web-rev");
                AssertComponent(root.GetProperty("ronFlowApi"), "RonFlow.Api", "20260530.101500", "ronflow-api-rev");
                AssertComponent(root.GetProperty("ronAuthApi"), "RonAuth.Api", "20260530.101500", "ronauth-api-rev");
            });
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public async Task GetBuildInfo_WhenComponentDeploymentDiffers_SetsIsSameDeploymentToFalse()
    {
        using var anonymousClient = CreateAnonymousClient();
        using var fixture = DeploymentSummaryFixture.Create(frontendVersion: "20260530.101500", ronFlowApiVersion: "20260530.101500", ronAuthApiVersion: "20260530.111500");

        fixture.Apply();

        try
        {
            var response = await anonymousClient.GetAsync("/api/system/build-info");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            await using var payloadStream = await response.Content.ReadAsStreamAsync();
            using var payload = await JsonDocument.ParseAsync(payloadStream);

            Assert.That(payload.RootElement.GetProperty("isSameDeployment").GetBoolean(), Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static void AssertComponent(JsonElement component, string application, string version, string sourceRevision)
    {
        Assert.That(component.GetProperty("application").GetString(), Is.EqualTo(application));
        Assert.That(component.GetProperty("version").GetString(), Is.EqualTo(version));
        Assert.That(component.GetProperty("informationalVersion").GetString(), Is.EqualTo($"{version}+{sourceRevision}"));
        Assert.That(component.GetProperty("sourceRevision").GetString(), Is.EqualTo(sourceRevision));

        var updatedAtUtc = component.GetProperty("updatedAtUtc").GetDateTimeOffset();
        Assert.That(updatedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(updatedAtUtc.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    private sealed class DeploymentSummaryFixture : IDisposable
    {
        private const string FrontendPathVariable = "RONFLOW_DEPLOYMENT_SUMMARY_FRONTEND_BUILD_INFO_PATH";
        private const string RonFlowApiPathVariable = "RONFLOW_DEPLOYMENT_SUMMARY_RONFLOW_API_BUILD_INFO_PATH";
        private const string RonAuthApiPathVariable = "RONFLOW_DEPLOYMENT_SUMMARY_RONAUTH_API_BUILD_INFO_PATH";

        private readonly string rootPath;
        private readonly string? originalFrontendPath;
        private readonly string? originalRonFlowApiPath;
        private readonly string? originalRonAuthApiPath;

        private DeploymentSummaryFixture(string rootPath)
        {
            this.rootPath = rootPath;
            originalFrontendPath = Environment.GetEnvironmentVariable(FrontendPathVariable);
            originalRonFlowApiPath = Environment.GetEnvironmentVariable(RonFlowApiPathVariable);
            originalRonAuthApiPath = Environment.GetEnvironmentVariable(RonAuthApiPathVariable);
        }

        public static DeploymentSummaryFixture Create(
            string frontendVersion = "20260530.101500",
            string ronFlowApiVersion = "20260530.101500",
            string ronAuthApiVersion = "20260530.101500")
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "ronflow-system-build-info-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            WriteBuildInfo(Path.Combine(rootPath, "ronflow-web.json"), "RonFlow.Web", frontendVersion, "ronflow-web-rev");
            WriteBuildInfo(Path.Combine(rootPath, "ronflow-api.json"), "RonFlow.Api", ronFlowApiVersion, "ronflow-api-rev");
            WriteBuildInfo(Path.Combine(rootPath, "ronauth-api.json"), "RonAuth.Api", ronAuthApiVersion, "ronauth-api-rev");

            return new DeploymentSummaryFixture(rootPath);
        }

        public void Apply()
        {
            Environment.SetEnvironmentVariable(FrontendPathVariable, Path.Combine(rootPath, "ronflow-web.json"));
            Environment.SetEnvironmentVariable(RonFlowApiPathVariable, Path.Combine(rootPath, "ronflow-api.json"));
            Environment.SetEnvironmentVariable(RonAuthApiPathVariable, Path.Combine(rootPath, "ronauth-api.json"));
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(FrontendPathVariable, originalFrontendPath);
            Environment.SetEnvironmentVariable(RonFlowApiPathVariable, originalRonFlowApiPath);
            Environment.SetEnvironmentVariable(RonAuthApiPathVariable, originalRonAuthApiPath);

            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }

        private static void WriteBuildInfo(string filePath, string application, string version, string sourceRevision)
        {
            var updatedAtUtc = new DateTimeOffset(2026, 05, 30, 10, 15, 00, TimeSpan.Zero);
            var payload = new
            {
                application,
                version,
                informationalVersion = $"{version}+{sourceRevision}",
                updatedAtUtc = updatedAtUtc.ToString("O"),
                sourceRevision,
            };

            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(filePath, JsonSerializer.Serialize(payload));
        }
    }
}