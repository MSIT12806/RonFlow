using Microsoft.Data.Sqlite;
using RonFlow.Infrastructure;

namespace RonFlow.Infrastructure.Tests;

public sealed class SqliteDatabaseSnapshotStoreTests
{
    [Test]
    public void WriteSnapshot_CreatesConsistentSqliteCopy()
    {
        using var temp = new TempDirectory();
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        var snapshotPath = Path.Combine(temp.Path, "repo", "ronflow.db");
        CreateDatabase(runtimeDatabasePath, "from runtime");

        new SqliteDatabaseSnapshotStore().WriteSnapshot(runtimeDatabasePath, snapshotPath);

        Assert.That(ReadValue(snapshotPath), Is.EqualTo("from runtime"));
    }

    [Test]
    public void RestoreSnapshot_CopiesRepositoryDatabaseToRuntimePath()
    {
        using var temp = new TempDirectory();
        var snapshotPath = Path.Combine(temp.Path, "repo", "ronflow.db");
        var runtimeDatabasePath = Path.Combine(temp.Path, "runtime", "ronflow.db");
        CreateDatabase(snapshotPath, "from repo");

        new SqliteDatabaseSnapshotStore().RestoreSnapshot(snapshotPath, runtimeDatabasePath);

        Assert.That(ReadValue(runtimeDatabasePath), Is.EqualTo("from repo"));
    }

    private static void CreateDatabase(string databasePath, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE Items (Value TEXT NOT NULL);
INSERT INTO Items (Value) VALUES ($value);";
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static string ReadValue(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Items LIMIT 1";
        return (string)command.ExecuteScalar()!;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronflow-sqlite-snapshot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
