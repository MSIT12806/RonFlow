using Microsoft.Data.Sqlite;
using RonFlow.Infrastructure;

namespace RonFlow.Infrastructure.Tests;

public sealed class SqliteCoreFlowStoreTests
{
    [Test]
    public void Constructor_CreatesDatabaseFileAndInitializesSchema()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "App_Data", "ronflow.db");

        using (new SqliteCoreFlowStore(databasePath).OpenConnection())
        {
        }

        Assert.That(File.Exists(databasePath), Is.True);
        AssertTableExists(databasePath, "Projects");
        AssertTableExists(databasePath, "Tasks");
        AssertTableExists(databasePath, "KnownUsers");
    }

    private static void AssertTableExists(string databasePath, string tableName)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName";
        command.Parameters.AddWithValue("$tableName", tableName);

        Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronflow-sqlite-store-tests", Guid.NewGuid().ToString("N"));
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
