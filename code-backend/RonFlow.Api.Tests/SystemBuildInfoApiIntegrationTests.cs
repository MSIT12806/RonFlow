using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class SystemBuildInfoApiIntegrationTests : ApiIntegrationTestBase
{
    [Test]
    public async Task GetBuildInfo_WhenAnonymous_ReturnsPublicBuildMetadata()
    {
        using var anonymousClient = CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/system/build-info");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<BuildInfoResponse>();

        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.Application, Is.Not.Empty);
            Assert.That(payload.EnvironmentName, Is.EqualTo("Testing"));
            Assert.That(payload.Version, Is.Not.Empty);
            Assert.That(payload.InformationalVersion, Is.Not.Empty);
            Assert.That(payload.UpdatedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(payload.UpdatedAtUtc.Offset, Is.EqualTo(TimeSpan.Zero));
        });
    }
}