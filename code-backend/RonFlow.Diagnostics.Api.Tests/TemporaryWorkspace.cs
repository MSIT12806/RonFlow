namespace RonFlow.Diagnostics.Api.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
    public TemporaryWorkspace()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronflow-diagnostics-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            ClearReadOnlyAttributes(Path);
            Directory.Delete(Path, recursive: true);
        }
    }

    private static void ClearReadOnlyAttributes(string path)
    {
        foreach (var filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(directoryPath, FileAttributes.Directory);
        }
    }
}
