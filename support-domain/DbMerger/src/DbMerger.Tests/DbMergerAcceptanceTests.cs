using DbMerger.Domain;
using Microsoft.Data.Sqlite;

namespace DbMerger.Tests;

public sealed class DbMergerAcceptanceTests
{
    [Test]
    public void Merge_KeyedUnionRecords_WritesLocalAndRemoteRows()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        CreateGenericRecordsDatabase(localPath, [new GenericRecord("local-only", "local-data")]);
        CreateGenericRecordsDatabase(remotePath, [new GenericRecord("remote-only", "remote-data")]);

        var result = new DbMergeService().Merge(new DbMergeRequest(
            localPath,
            remotePath,
            outputPath,
            DbMergeRecipeIds.GenericKeyedRecords,
            ConflictResolutionPolicy.LocalWin()));

        Assert.That(result.Status, Is.EqualTo(DbMergeStatus.Succeeded));
        Assert.That(ReadGenericRecords(outputPath), Is.EquivalentTo(new[]
        {
            new GenericRecord("local-only", "local-data"),
            new GenericRecord("remote-only", "remote-data"),
        }));
        Assert.That(result.Report.Tables.Single(table => table.TableName == "Records").InsertedCount, Is.EqualTo(2));
    }

    [Test]
    public void Merge_SameIdentityConflict_WithLocalWin_WritesLocalRow()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        CreateGenericRecordsDatabase(localPath, [new GenericRecord("shared", "local-data")]);
        CreateGenericRecordsDatabase(remotePath, [new GenericRecord("shared", "remote-data")]);

        var result = new DbMergeService().Merge(new DbMergeRequest(
            localPath,
            remotePath,
            outputPath,
            DbMergeRecipeIds.GenericKeyedRecords,
            ConflictResolutionPolicy.LocalWin()));

        Assert.That(result.Status, Is.EqualTo(DbMergeStatus.Succeeded));
        Assert.That(ReadGenericRecords(outputPath), Is.EqualTo(new[] { new GenericRecord("shared", "local-data") }));
        Assert.That(result.Report.ConflictEntries.Single().AppliedPolicy, Is.EqualTo(ConflictResolutionKind.LocalWin));
        Assert.That(result.Report.ConflictEntries.Single().Outcome, Is.EqualTo("UseLocal"));
    }

    [Test]
    public void Merge_SameIdentityConflict_WithRemoteWin_WritesRemoteRow()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        CreateGenericRecordsDatabase(localPath, [new GenericRecord("shared", "local-data")]);
        CreateGenericRecordsDatabase(remotePath, [new GenericRecord("shared", "remote-data")]);

        var result = new DbMergeService().Merge(new DbMergeRequest(
            localPath,
            remotePath,
            outputPath,
            DbMergeRecipeIds.GenericKeyedRecords,
            ConflictResolutionPolicy.RemoteWin()));

        Assert.That(result.Status, Is.EqualTo(DbMergeStatus.Succeeded));
        Assert.That(ReadGenericRecords(outputPath), Is.EqualTo(new[] { new GenericRecord("shared", "remote-data") }));
        Assert.That(result.Report.ConflictEntries.Single().AppliedPolicy, Is.EqualTo(ConflictResolutionKind.RemoteWin));
        Assert.That(result.Report.ConflictEntries.Single().Outcome, Is.EqualTo("UseRemote"));
    }

    [Test]
    public void Merge_RonFlowKnownUsersIdentityDrift_FailsWithUnresolvedConflict()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        CreateRonFlowKnownUsersDatabase(localPath, [new KnownUserRecord("local-user-id", "robin", "robin@example.test")]);
        CreateRonFlowKnownUsersDatabase(remotePath, [new KnownUserRecord("remote-user-id", "robin", "robin@example.test")]);

        var result = new DbMergeService().Merge(new DbMergeRequest(
            localPath,
            remotePath,
            outputPath,
            DbMergeRecipeIds.RonFlow,
            ConflictResolutionPolicy.LocalWin()));

        Assert.That(result.Status, Is.EqualTo(DbMergeStatus.Failed));
        Assert.That(File.Exists(outputPath), Is.False);
        Assert.That(result.Report.ConflictEntries.Single().Scenario, Is.EqualTo("IdentityDrift"));
        Assert.That(result.Report.ConflictEntries.Single().Outcome, Is.EqualTo("Unresolved"));
    }

    [Test]
    public void Merge_RonFlowSnapshots_UnionsProjectsAndTasksIntoMergedDatabase()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        CreateRonFlowCoreDatabase(
            localPath,
            projects: [new KeyedJsonRecord("local-project", """{"id":"local-project","updatedAt":"2026-07-02T00:00:00+00:00"}""")],
            tasks: [new KeyedJsonRecord("local-task", """{"id":"local-task","projectId":"local-project"}""")]);
        CreateRonFlowCoreDatabase(
            remotePath,
            projects: [new KeyedJsonRecord("remote-project", """{"id":"remote-project","updatedAt":"2026-07-02T00:00:00+00:00"}""")],
            tasks: [new KeyedJsonRecord("remote-task", """{"id":"remote-task","projectId":"remote-project"}""")]);

        var result = new DbMergeService().Merge(new DbMergeRequest(
            localPath,
            remotePath,
            outputPath,
            DbMergeRecipeIds.RonFlow,
            ConflictResolutionPolicy.LocalWin()));

        Assert.That(result.Status, Is.EqualTo(DbMergeStatus.Succeeded));
        Assert.That(ReadKeyedJsonRecords(outputPath, "Projects", "Id"), Is.EquivalentTo(new[]
        {
            new KeyedJsonRecord("local-project", """{"id":"local-project","updatedAt":"2026-07-02T00:00:00+00:00"}"""),
            new KeyedJsonRecord("remote-project", """{"id":"remote-project","updatedAt":"2026-07-02T00:00:00+00:00"}"""),
        }));
        Assert.That(ReadKeyedJsonRecords(outputPath, "Tasks", "Id"), Is.EquivalentTo(new[]
        {
            new KeyedJsonRecord("local-task", """{"id":"local-task","projectId":"local-project"}"""),
            new KeyedJsonRecord("remote-task", """{"id":"remote-task","projectId":"remote-project"}"""),
        }));
    }

    [Test]
    public void Merge_DoesNotModifyInputSnapshots()
    {
        using var temp = new TempDirectory();
        var localPath = temp.DatabasePath("local.db");
        var remotePath = temp.DatabasePath("remote.db");
        var outputPath = temp.DatabasePath("merged.db");
        CreateGenericRecordsDatabase(localPath, [new GenericRecord("local-only", "local-data")]);
        CreateGenericRecordsDatabase(remotePath, [new GenericRecord("remote-only", "remote-data")]);
        var localBefore = File.ReadAllBytes(localPath);
        var remoteBefore = File.ReadAllBytes(remotePath);

        _ = new DbMergeService().Merge(new DbMergeRequest(
            localPath,
            remotePath,
            outputPath,
            DbMergeRecipeIds.GenericKeyedRecords,
            ConflictResolutionPolicy.LocalWin()));

        Assert.That(File.ReadAllBytes(localPath), Is.EqualTo(localBefore));
        Assert.That(File.ReadAllBytes(remotePath), Is.EqualTo(remoteBefore));
    }

    private static void CreateGenericRecordsDatabase(string path, IEnumerable<GenericRecord> records)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE Records (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);";
        command.ExecuteNonQuery();

        foreach (var record in records)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO Records (Id, Data) VALUES ($id, $data)";
            insert.Parameters.AddWithValue("$id", record.Id);
            insert.Parameters.AddWithValue("$data", record.Data);
            insert.ExecuteNonQuery();
        }
    }

    private static IReadOnlyList<GenericRecord> ReadGenericRecords(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Data FROM Records ORDER BY Id";
        using var reader = command.ExecuteReader();
        var records = new List<GenericRecord>();
        while (reader.Read())
        {
            records.Add(new GenericRecord(reader.GetString(0), reader.GetString(1)));
        }

        return records;
    }

    private static void CreateRonFlowKnownUsersDatabase(string path, IEnumerable<KnownUserRecord> users)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE KnownUsers (
    UserId TEXT NOT NULL PRIMARY KEY,
    UserName TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE
);";
        command.ExecuteNonQuery();

        foreach (var user in users)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO KnownUsers (UserId, UserName, Email) VALUES ($userId, $userName, $email)";
            insert.Parameters.AddWithValue("$userId", user.UserId);
            insert.Parameters.AddWithValue("$userName", user.UserName);
            insert.Parameters.AddWithValue("$email", user.Email);
            insert.ExecuteNonQuery();
        }
    }

    private static void CreateRonFlowCoreDatabase(
        string path,
        IEnumerable<KeyedJsonRecord> projects,
        IEnumerable<KeyedJsonRecord> tasks)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE Projects (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE Tasks (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);";
        command.ExecuteNonQuery();

        InsertKeyedJsonRecords(connection, "Projects", projects);
        InsertKeyedJsonRecords(connection, "Tasks", tasks);
    }

    private static void InsertKeyedJsonRecords(SqliteConnection connection, string tableName, IEnumerable<KeyedJsonRecord> records)
    {
        foreach (var record in records)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = $"INSERT INTO {tableName} (Id, Data) VALUES ($id, $data)";
            insert.Parameters.AddWithValue("$id", record.Id);
            insert.Parameters.AddWithValue("$data", record.Data);
            insert.ExecuteNonQuery();
        }
    }

    private static IReadOnlyList<KeyedJsonRecord> ReadKeyedJsonRecords(string path, string tableName, string keyColumnName)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {keyColumnName}, Data FROM {tableName} ORDER BY {keyColumnName}";
        using var reader = command.ExecuteReader();
        var records = new List<KeyedJsonRecord>();
        while (reader.Read())
        {
            records.Add(new KeyedJsonRecord(reader.GetString(0), reader.GetString(1)));
        }

        return records;
    }

    private sealed record GenericRecord(string Id, string Data);

    private sealed record KnownUserRecord(string UserId, string UserName, string Email);

    private sealed record KeyedJsonRecord(string Id, string Data);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dbmerger-acceptance-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string DatabasePath(string fileName)
        {
            return System.IO.Path.Combine(Path, fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
