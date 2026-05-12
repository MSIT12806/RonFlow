using Microsoft.Data.Sqlite;

namespace RonFlow.Infrastructure;

public sealed class SqliteCoreFlowStore
{
    private readonly string connectionString;

    public SqliteCoreFlowStore(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        EnsureInitialized();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureInitialized()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Projects (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Tasks (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);";

        command.ExecuteNonQuery();
    }
}