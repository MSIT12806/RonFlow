using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RonFlow.Diagnostics.Api.Tests;

public sealed class LogSourceReaderTests
{
    [Test]
    public async Task ReadTailAsync_EnforcesConfiguredTailLimitAndRedactsLines()
    {
        using var workspace = new TemporaryWorkspace();
        var logPath = Path.Combine(workspace.Path, "app.log");
        await File.WriteAllLinesAsync(logPath, Enumerable.Range(1, 5).Select(index => index == 5
            ? "Bearer abc.def.ghi and Password=hunter2"
            : $"line {index}"));
        var reader = CreateReader(new DiagnosticsOptions
        {
            MaxTailLines = 3,
            LogSources = new(StringComparer.OrdinalIgnoreCase)
            {
                ["app"] = new LogSourceOptions
                {
                    PathPattern = logPath,
                    DisplayName = "App log",
                },
            },
        });

        var result = await reader.ReadTailAsync("app", 999, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Tail, Is.EqualTo(3));
        Assert.That(result.Lines, Is.EqualTo(new[] { "line 3", "line 4", "Bearer *** and Password=***" }));
    }

    [Test]
    public async Task ReadTailAsync_CentralizedProvider_MapsBoundedQueryAndRedactsReturnedLines()
    {
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                hits = new
                {
                    hits = new object[]
                    {
                        new
                        {
                            _source = new
                            {
                                timestamp = "2026-06-14T00:00:00Z",
                                message = "Bearer abc.def.ghi",
                            },
                        },
                    },
                },
            }),
        });
        var reader = CreateReader(new DiagnosticsOptions
        {
            MaxTailLines = 3,
            LogSources = new(StringComparer.OrdinalIgnoreCase)
            {
                ["central"] = new LogSourceOptions
                {
                    Provider = LogSourceProviderKind.Elasticsearch,
                    DisplayName = "Central logs",
                    Centralized = new CentralizedLogSourceOptions
                    {
                        Endpoint = "https://logs.example.test",
                        IndexPattern = "ronflow-*",
                        ServiceName = "ronflow-api",
                        Environment = "localhost",
                        TimeRangeMinutes = 30,
                    },
                },
            },
        }, handler);

        var result = await reader.ReadTailAsync("central", 999, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Exists, Is.True);
        Assert.That(result.Readable, Is.True);
        Assert.That(result.Tail, Is.EqualTo(3));
        Assert.That(result.Lines, Is.EqualTo(new[] { "2026-06-14T00:00:00Z Bearer ***" }));
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        Assert.That(handler.Requests[0].RequestUri?.ToString(), Is.EqualTo("https://logs.example.test/ronflow-*/_search"));

        using var payload = JsonDocument.Parse(handler.Bodies[0]);
        Assert.That(payload.RootElement.GetProperty("size").GetInt32(), Is.EqualTo(3));
        var filters = payload.RootElement
            .GetProperty("query")
            .GetProperty("bool")
            .GetProperty("filter")
            .EnumerateArray()
            .ToArray();
        Assert.That(filters.Any(filter => filter.ToString().Contains("ronflow-api", StringComparison.Ordinal)), Is.True);
        Assert.That(filters.Any(filter => filter.ToString().Contains("localhost", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task ReadTailAsync_CentralizedProviderFailure_ReturnsStructuredRedactedError()
    {
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("downstream failed Password=hunter2"),
        });
        var reader = CreateReader(new DiagnosticsOptions
        {
            LogSources = new(StringComparer.OrdinalIgnoreCase)
            {
                ["central"] = new LogSourceOptions
                {
                    Provider = LogSourceProviderKind.Elasticsearch,
                    DisplayName = "Central logs",
                    Centralized = new CentralizedLogSourceOptions
                    {
                        Endpoint = "https://token@logs.example.test",
                        IndexPattern = "ronflow-*",
                    },
                },
            },
        }, handler);

        var result = await reader.ReadTailAsync("central", 200, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Exists, Is.True);
        Assert.That(result.Readable, Is.False);
        Assert.That(result.Lines, Is.Empty);
        Assert.That(result.Error, Does.Contain("500"));
        Assert.That(result.Error, Does.Contain("Password=***"));
        Assert.That(result.Error, Does.Not.Contain("hunter2"));
        Assert.That(result.Error, Does.Not.Contain("token@"));
    }

    [Test]
    public async Task ReadTailAsync_MissingConfiguredFile_ReturnsStructuredExistsFalse()
    {
        using var workspace = new TemporaryWorkspace();
        var reader = CreateReader(new DiagnosticsOptions
        {
            LogSources = new(StringComparer.OrdinalIgnoreCase)
            {
                ["missing"] = new LogSourceOptions
                {
                    PathPattern = Path.Combine(workspace.Path, "missing.log"),
                    DisplayName = "Missing log",
                },
            },
        });

        var result = await reader.ReadTailAsync("missing", 200, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Exists, Is.False);
        Assert.That(result.Readable, Is.False);
        Assert.That(result.Lines, Is.Empty);
    }

    private static Api.LogSourceReader CreateReader(
        DiagnosticsOptions options,
        CapturingHttpMessageHandler? handler = null)
    {
        var redactor = new Api.LogRedactor();
        var httpClient = new HttpClient(handler ?? new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)))
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
        return new Api.LogSourceReader(
            Options.Create(options),
            [
                new Api.FileLogSourceProvider(redactor),
                new Api.CentralizedLogSourceProvider(httpClient, redactor),
            ],
            redactor);
    }

    private sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return response;
        }
    }
}
