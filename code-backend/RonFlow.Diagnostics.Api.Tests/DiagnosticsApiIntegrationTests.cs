using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RonFlow.Diagnostics.Api.Tests;

public sealed class DiagnosticsApiIntegrationTests
{
    [Test]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        await using var factory = new WebApplicationFactory<Api.Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body, Does.Contain("RonFlow.Diagnostics.Api"));
        Assert.That(body, Does.Contain("healthy"));
    }

    [Test]
    public async Task UnknownLogSource_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Api.Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/logs/missing");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
