using Microsoft.Extensions.Options;

namespace RonFlow.Diagnostics.Api.Tests;

public sealed class ConfiguredHealthCheckRunnerTests
{
    [Test]
    public async Task RunAsync_TreatsConfiguredExpectedStatusCodeAsHealthy()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(System.Net.HttpStatusCode.Unauthorized));
        var runner = new Api.ConfiguredHealthCheckRunner(httpClient, Options.Create(new Api.DiagnosticsOptions
        {
            HealthChecks = new(StringComparer.OrdinalIgnoreCase)
            {
                ["auth"] = new Api.HealthCheckSourceOptions
                {
                    Url = "http://localhost/ronauth-api/api/auth/login",
                    DisplayName = "RonAuth",
                    ExpectedStatusCodes = [401],
                },
            },
        }), new Api.LogRedactor(), TimeProvider.System);

        var result = await runner.RunAsync("auth", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(401));
        Assert.That(result.IsExpectedStatusCode, Is.True);
        Assert.That(result.Host, Is.EqualTo("localhost"));
        Assert.That(result.Path, Is.EqualTo("/ronauth-api/api/auth/login"));
    }

    private sealed class StubHttpMessageHandler(System.Net.HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
