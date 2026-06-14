using Microsoft.Extensions.Options;

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

    private static Api.FileLogSourceReader CreateReader(DiagnosticsOptions options) =>
        new(Options.Create(options), new Api.LogRedactor());
}
