using System.Diagnostics;
using RonFlow.Infrastructure;

namespace RonFlow.Infrastructure.Tests;

public sealed class GitDatabaseRepositorySyncTests
{
    [Test]
    public void CommitAndPush_CommitsChangedDatabaseFile()
    {
        using var temp = new TempDirectory();
        var repositoryPath = Path.Combine(temp.Path, "repo");
        var options = new DatabaseSyncOptions
        {
            Enabled = true,
            RepositoryPath = repositoryPath,
            Branch = "main",
        };
        var sync = new GitDatabaseRepositorySync(options);
        sync.EnsureReady();
        RunGit(repositoryPath, "config", "user.email", "ronflow-test@example.test");
        RunGit(repositoryPath, "config", "user.name", "RonFlow Test");
        File.WriteAllText(Path.Combine(repositoryPath, "ronflow.db"), "database snapshot");

        sync.CommitAndPush("ronflow.db", "Sync database");

        Assert.That(ShowGitFile(repositoryPath, "ronflow.db"), Is.EqualTo("database snapshot"));
    }

    private static string ShowGitFile(string repositoryPath, string filePath)
    {
        return RunGit(repositoryPath, "show", $"HEAD:{filePath}").TrimEnd('\r', '\n');
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var processStartInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start git.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.That(process.ExitCode, Is.EqualTo(0), $"git {string.Join(' ', arguments)} failed: {standardError}");
        return standardOutput;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronflow-git-sync-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                ResetFileAttributes(Path);
                TryDelete(Path);
            }
        }

        private static void ResetFileAttributes(string path)
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(directory, FileAttributes.Normal);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
