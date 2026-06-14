using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace RonFlow.Diagnostics.Api.Tests;

public sealed class GitRepositoryInspectorTests
{
    [Test]
    public async Task GetStatusAsync_ReturnsCleanAndDirtyStateForConfiguredRepository()
    {
        using var workspace = new TemporaryWorkspace();
        await RunGitAsync(workspace.Path, "init");
        await RunGitAsync(workspace.Path, "config", "user.email", "diagnostics@example.test");
        await RunGitAsync(workspace.Path, "config", "user.name", "Diagnostics Test");
        var trackedFile = Path.Combine(workspace.Path, "tracked.txt");
        await File.WriteAllTextAsync(trackedFile, "clean");
        await RunGitAsync(workspace.Path, "add", "tracked.txt");
        await RunGitAsync(workspace.Path, "commit", "-m", "initial commit");
        var inspector = CreateInspector(workspace.Path);

        var clean = await inspector.GetStatusAsync("repo", CancellationToken.None);
        await File.WriteAllTextAsync(trackedFile, "dirty");
        var dirty = await inspector.GetStatusAsync("repo", CancellationToken.None);

        Assert.That(clean, Is.Not.Null);
        Assert.That(clean!.Exists, Is.True);
        Assert.That(clean.IsRepository, Is.True);
        Assert.That(clean.WorkingTreeClean, Is.True);
        Assert.That(clean.LatestCommit, Is.Not.Null);
        Assert.That(dirty, Is.Not.Null);
        Assert.That(dirty!.WorkingTreeClean, Is.False);
        Assert.That(dirty.StatusLines, Has.Some.Contains("tracked.txt"));
    }

    private static Api.GitRepositoryInspector CreateInspector(string repositoryPath) =>
        new(Options.Create(new Api.DiagnosticsOptions
        {
            GitRepositories = new(StringComparer.OrdinalIgnoreCase)
            {
                ["repo"] = new Api.GitRepositoryOptions
                {
                    Path = repositoryPath,
                    DisplayName = "Repository",
                },
            },
        }), new Api.LogRedactor(), TimeProvider.System);

    private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(await errorTask);
        }
    }
}
