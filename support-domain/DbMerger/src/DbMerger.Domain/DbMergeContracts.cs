namespace DbMerger.Domain;

using Microsoft.Data.Sqlite;

public static class DbMergeRecipeIds
{
    public const string GenericKeyedRecords = "generic-keyed-records";

    public const string RonFlow = "ronflow";
}

public sealed record DbMergeRequest(
    string LocalSnapshotPath,
    string RemoteSnapshotPath,
    string OutputSnapshotPath,
    string RecipeId,
    ConflictResolutionPolicy ConflictResolutionPolicy,
    bool FailOnUnresolvedConflict = true);

public sealed record ConflictResolutionPolicy(ConflictResolutionKind Kind)
{
    public static ConflictResolutionPolicy LocalWin()
    {
        return new ConflictResolutionPolicy(ConflictResolutionKind.LocalWin);
    }

    public static ConflictResolutionPolicy RemoteWin()
    {
        return new ConflictResolutionPolicy(ConflictResolutionKind.RemoteWin);
    }

    public static ConflictResolutionPolicy Fail()
    {
        return new ConflictResolutionPolicy(ConflictResolutionKind.Fail);
    }
}

public enum ConflictResolutionKind
{
    LocalWin,
    RemoteWin,
    Fail,
}

public enum DbMergeStatus
{
    Succeeded,
    Failed,
    CompletedWithUnresolvedConflicts,
}

public sealed record DbMergeResult(
    DbMergeStatus Status,
    string? OutputSnapshotPath,
    DbMergeReport Report,
    string? ErrorMessage = null);

public sealed record DbMergeReport(
    string RecipeId,
    IReadOnlyList<TableMergeReport> Tables,
    IReadOnlyList<ConflictEntry> ConflictEntries);

public sealed record TableMergeReport(
    string TableName,
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount,
    int ResolvedConflictCount,
    int UnresolvedConflictCount);

public sealed record ConflictEntry(
    string TableName,
    string RecordIdentity,
    string Scenario,
    ConflictResolutionKind? AppliedPolicy,
    string Outcome,
    string Message);

public sealed class DbMergeService
{
    public DbMergeResult Merge(DbMergeRequest request)
    {
        return request.RecipeId switch
        {
            DbMergeRecipeIds.GenericKeyedRecords => MergeGenericKeyedRecords(request),
            DbMergeRecipeIds.RonFlow => MergeRonFlow(request),
            _ => Failed(request, $"Unsupported recipe id '{request.RecipeId}'."),
        };
    }

    private static DbMergeResult MergeGenericKeyedRecords(DbMergeRequest request)
    {
        var localRecords = ReadKeyedRecords(request.LocalSnapshotPath, "Records", "Id", "Data");
        var remoteRecords = ReadKeyedRecords(request.RemoteSnapshotPath, "Records", "Id", "Data");
        var mergedRecords = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new List<ConflictEntry>();
        var insertedCount = 0;
        var unchangedCount = 0;
        var resolvedConflictCount = 0;

        foreach (var id in localRecords.Keys.Union(remoteRecords.Keys).Order(StringComparer.Ordinal))
        {
            var hasLocal = localRecords.TryGetValue(id, out var localData);
            var hasRemote = remoteRecords.TryGetValue(id, out var remoteData);

            if (hasLocal && hasRemote)
            {
                if (string.Equals(localData, remoteData, StringComparison.Ordinal))
                {
                    mergedRecords[id] = localData!;
                    unchangedCount += 1;
                    continue;
                }

                var resolution = ResolveContentConflict(request.ConflictResolutionPolicy, id, localData!, remoteData!);
                if (resolution.UnresolvedConflict is not null)
                {
                    conflicts.Add(resolution.UnresolvedConflict);
                    continue;
                }

                conflicts.Add(resolution.ResolvedConflict!);
                mergedRecords[id] = resolution.Value!;
                resolvedConflictCount += 1;
                continue;
            }

            mergedRecords[id] = hasLocal ? localData! : remoteData!;
            insertedCount += 1;
        }

        if (conflicts.Any(conflict => conflict.Outcome == "Unresolved") && request.FailOnUnresolvedConflict)
        {
            DeleteIfExists(request.OutputSnapshotPath);
            return new DbMergeResult(
                DbMergeStatus.Failed,
                null,
                new DbMergeReport(
                    request.RecipeId,
                    [new TableMergeReport("Records", insertedCount, 0, unchangedCount, resolvedConflictCount, conflicts.Count(conflict => conflict.Outcome == "Unresolved"))],
                    conflicts),
                "Unresolved conflicts were found.");
        }

        WriteGenericRecords(request.OutputSnapshotPath, mergedRecords);
        return new DbMergeResult(
            DbMergeStatus.Succeeded,
            request.OutputSnapshotPath,
            new DbMergeReport(
                request.RecipeId,
                [new TableMergeReport("Records", insertedCount, 0, unchangedCount, resolvedConflictCount, 0)],
                conflicts));
    }

    private static DbMergeResult MergeRonFlow(DbMergeRequest request)
    {
        var localUsers = ReadKnownUsers(request.LocalSnapshotPath);
        var remoteUsers = ReadKnownUsers(request.RemoteSnapshotPath);
        var conflicts = DetectKnownUserIdentityDrift(localUsers, remoteUsers);

        if (conflicts.Count > 0 && request.FailOnUnresolvedConflict)
        {
            DeleteIfExists(request.OutputSnapshotPath);
            return new DbMergeResult(
                DbMergeStatus.Failed,
                null,
                new DbMergeReport(
                    request.RecipeId,
                    [new TableMergeReport("KnownUsers", 0, 0, 0, 0, conflicts.Count)],
                    conflicts),
                "Unresolved identity drift conflicts were found.");
        }

        var mergedUsers = MergeKnownUsersByUserId(localUsers, remoteUsers, request.ConflictResolutionPolicy, conflicts);
        var tableReports = new List<TableMergeReport>
        {
            new("KnownUsers", mergedUsers.Count, 0, 0, 0, conflicts.Count)
        };
        var mergedTables = new Dictionary<string, IReadOnlyList<DatabaseRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in RonFlowTableSpecs)
        {
            var merge = MergeTableRows(request, table);
            mergedTables[table.Name] = merge.Rows;
            tableReports.Add(merge.Report);
            conflicts.AddRange(merge.Conflicts);
        }

        if (conflicts.Any(conflict => conflict.Outcome == "Unresolved") && request.FailOnUnresolvedConflict)
        {
            DeleteIfExists(request.OutputSnapshotPath);
            return new DbMergeResult(
                DbMergeStatus.Failed,
                null,
                new DbMergeReport(request.RecipeId, tableReports, conflicts),
                "Unresolved conflicts were found.");
        }

        WriteRonFlowTables(request.OutputSnapshotPath, mergedUsers, mergedTables);

        return new DbMergeResult(
            conflicts.Count == 0 ? DbMergeStatus.Succeeded : DbMergeStatus.CompletedWithUnresolvedConflicts,
            request.OutputSnapshotPath,
            new DbMergeReport(
                request.RecipeId,
                tableReports,
                conflicts));
    }

    private static ConflictResolution ResolveContentConflict(ConflictResolutionPolicy policy, string id, string localData, string remoteData)
    {
        return policy.Kind switch
        {
            ConflictResolutionKind.LocalWin => new ConflictResolution(
                localData,
                new ConflictEntry("Records", id, "SameIdentityDifferentContent", ConflictResolutionKind.LocalWin, "UseLocal", "Local record selected by policy."),
                null),
            ConflictResolutionKind.RemoteWin => new ConflictResolution(
                remoteData,
                new ConflictEntry("Records", id, "SameIdentityDifferentContent", ConflictResolutionKind.RemoteWin, "UseRemote", "Remote record selected by policy."),
                null),
            _ => new ConflictResolution(
                null,
                null,
                new ConflictEntry("Records", id, "SameIdentityDifferentContent", policy.Kind, "Unresolved", "Conflict policy did not select a record.")),
        };
    }

    private static SortedDictionary<string, string> ReadKeyedRecords(
        string databasePath,
        string tableName,
        string keyColumnName,
        string dataColumnName)
    {
        using var connection = OpenReadOnlyConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {QuoteIdentifier(keyColumnName)}, {QuoteIdentifier(dataColumnName)} FROM {QuoteIdentifier(tableName)}";
        using var reader = command.ExecuteReader();
        var records = new SortedDictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            records[reader.GetString(0)] = reader.GetString(1);
        }

        return records;
    }

    private static IReadOnlyList<KnownUserSnapshotRecord> ReadKnownUsers(string databasePath)
    {
        using var connection = OpenReadOnlyConnection(databasePath);
        if (!TableExists(connection, "KnownUsers"))
        {
            return [];
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId, UserName, Email FROM KnownUsers";
        using var reader = command.ExecuteReader();
        var records = new List<KnownUserSnapshotRecord>();
        while (reader.Read())
        {
            records.Add(new KnownUserSnapshotRecord(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return records;
    }

    private static List<ConflictEntry> DetectKnownUserIdentityDrift(
        IReadOnlyList<KnownUserSnapshotRecord> localUsers,
        IReadOnlyList<KnownUserSnapshotRecord> remoteUsers)
    {
        var localByEmail = localUsers.ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<ConflictEntry>();

        foreach (var remoteUser in remoteUsers)
        {
            if (!localByEmail.TryGetValue(remoteUser.Email, out var localUser))
            {
                continue;
            }

            if (string.Equals(localUser.UserId, remoteUser.UserId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            conflicts.Add(new ConflictEntry(
                "KnownUsers",
                remoteUser.Email,
                "IdentityDrift",
                null,
                "Unresolved",
                $"KnownUsers email '{remoteUser.Email}' maps to local UserId '{localUser.UserId}' and remote UserId '{remoteUser.UserId}'."));
        }

        return conflicts;
    }

    private static IReadOnlyList<KnownUserSnapshotRecord> MergeKnownUsersByUserId(
        IReadOnlyList<KnownUserSnapshotRecord> localUsers,
        IReadOnlyList<KnownUserSnapshotRecord> remoteUsers,
        ConflictResolutionPolicy policy,
        ICollection<ConflictEntry> conflicts)
    {
        var merged = localUsers.ToDictionary(user => user.UserId, StringComparer.OrdinalIgnoreCase);
        foreach (var remoteUser in remoteUsers)
        {
            if (!merged.TryGetValue(remoteUser.UserId, out var localUser))
            {
                merged[remoteUser.UserId] = remoteUser;
                continue;
            }

            if (localUser == remoteUser)
            {
                continue;
            }

            switch (policy.Kind)
            {
                case ConflictResolutionKind.RemoteWin:
                    merged[remoteUser.UserId] = remoteUser;
                    conflicts.Add(new ConflictEntry("KnownUsers", remoteUser.UserId, "SameIdentityDifferentContent", policy.Kind, "UseRemote", "Remote KnownUsers record selected by policy."));
                    break;
                case ConflictResolutionKind.LocalWin:
                    conflicts.Add(new ConflictEntry("KnownUsers", remoteUser.UserId, "SameIdentityDifferentContent", policy.Kind, "UseLocal", "Local KnownUsers record selected by policy."));
                    break;
                default:
                    conflicts.Add(new ConflictEntry("KnownUsers", remoteUser.UserId, "SameIdentityDifferentContent", policy.Kind, "Unresolved", "Conflict policy did not select a KnownUsers record."));
                    break;
            }
        }

        return merged.Values
            .OrderBy(user => user.UserId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void WriteGenericRecords(string outputPath, IReadOnlyDictionary<string, string> records)
    {
        WriteOutputDatabase(outputPath, (connection) =>
        {
            using var create = connection.CreateCommand();
            create.CommandText = @"
CREATE TABLE Records (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);";
            create.ExecuteNonQuery();

            foreach (var record in records)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO Records (Id, Data) VALUES ($id, $data)";
                insert.Parameters.AddWithValue("$id", record.Key);
                insert.Parameters.AddWithValue("$data", record.Value);
                insert.ExecuteNonQuery();
            }
        });
    }

    private static void WriteKnownUsers(string outputPath, IReadOnlyList<KnownUserSnapshotRecord> users)
    {
        WriteOutputDatabase(outputPath, (connection) =>
        {
            using var create = connection.CreateCommand();
            create.CommandText = @"
CREATE TABLE KnownUsers (
    UserId TEXT NOT NULL PRIMARY KEY,
    UserName TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE
);";
            create.ExecuteNonQuery();

            foreach (var user in users)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO KnownUsers (UserId, UserName, Email) VALUES ($userId, $userName, $email)";
                insert.Parameters.AddWithValue("$userId", user.UserId);
                insert.Parameters.AddWithValue("$userName", user.UserName);
                insert.Parameters.AddWithValue("$email", user.Email);
                insert.ExecuteNonQuery();
            }
        });
    }

    private static TableMerge MergeTableRows(DbMergeRequest request, TableSpec table)
    {
        var localRows = ReadRows(request.LocalSnapshotPath, table);
        var remoteRows = ReadRows(request.RemoteSnapshotPath, table);
        var mergedRows = new List<DatabaseRow>();
        var conflicts = new List<ConflictEntry>();
        var insertedCount = 0;
        var unchangedCount = 0;
        var resolvedConflictCount = 0;

        foreach (var key in localRows.Keys.Union(remoteRows.Keys).Order(StringComparer.Ordinal))
        {
            var hasLocal = localRows.TryGetValue(key, out var localRow);
            var hasRemote = remoteRows.TryGetValue(key, out var remoteRow);

            if (hasLocal && hasRemote)
            {
                if (localRow!.ContentEquals(remoteRow!))
                {
                    mergedRows.Add(localRow);
                    unchangedCount += 1;
                    continue;
                }

                switch (request.ConflictResolutionPolicy.Kind)
                {
                    case ConflictResolutionKind.LocalWin:
                        mergedRows.Add(localRow!);
                        resolvedConflictCount += 1;
                        conflicts.Add(new ConflictEntry(table.Name, key, "SameIdentityDifferentContent", ConflictResolutionKind.LocalWin, "UseLocal", "Local record selected by policy."));
                        break;
                    case ConflictResolutionKind.RemoteWin:
                        mergedRows.Add(remoteRow!);
                        resolvedConflictCount += 1;
                        conflicts.Add(new ConflictEntry(table.Name, key, "SameIdentityDifferentContent", ConflictResolutionKind.RemoteWin, "UseRemote", "Remote record selected by policy."));
                        break;
                    default:
                        conflicts.Add(new ConflictEntry(table.Name, key, "SameIdentityDifferentContent", request.ConflictResolutionPolicy.Kind, "Unresolved", "Conflict policy did not select a record."));
                        break;
                }

                continue;
            }

            mergedRows.Add(hasLocal ? localRow! : remoteRow!);
            insertedCount += 1;
        }

        return new TableMerge(
            mergedRows,
            new TableMergeReport(
                table.Name,
                insertedCount,
                0,
                unchangedCount,
                resolvedConflictCount,
                conflicts.Count(conflict => conflict.Outcome == "Unresolved")),
            conflicts);
    }

    private static SortedDictionary<string, DatabaseRow> ReadRows(string databasePath, TableSpec table)
    {
        var rows = new SortedDictionary<string, DatabaseRow>(StringComparer.Ordinal);
        using var connection = OpenReadOnlyConnection(databasePath);
        if (!TableExists(connection, table.Name))
        {
            return rows;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {string.Join(", ", table.Columns.Select(QuoteIdentifier))} FROM {QuoteIdentifier(table.Name)}";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < table.Columns.Count; index += 1)
            {
                values[table.Columns[index]] = reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture);
            }

            var key = string.Join('\u001f', table.KeyColumns.Select(column => values[column] ?? string.Empty));
            rows[key] = new DatabaseRow(table.Name, values);
        }

        return rows;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() is not null;
    }

    private static void WriteRonFlowTables(
        string outputPath,
        IReadOnlyList<KnownUserSnapshotRecord> users,
        IReadOnlyDictionary<string, IReadOnlyList<DatabaseRow>> tables)
    {
        WriteOutputDatabase(outputPath, (connection) =>
        {
            using var create = connection.CreateCommand();
            create.CommandText = RonFlowSchemaSql;
            create.ExecuteNonQuery();

            foreach (var user in users)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO KnownUsers (UserId, UserName, Email) VALUES ($userId, $userName, $email)";
                insert.Parameters.AddWithValue("$userId", user.UserId);
                insert.Parameters.AddWithValue("$userName", user.UserName);
                insert.Parameters.AddWithValue("$email", user.Email);
                insert.ExecuteNonQuery();
            }

            foreach (var table in RonFlowTableSpecs)
            {
                if (!tables.TryGetValue(table.Name, out var rows))
                {
                    continue;
                }

                foreach (var row in rows)
                {
                    InsertRow(connection, table, row);
                }
            }
        });
    }

    private static void InsertRow(SqliteConnection connection, TableSpec table, DatabaseRow row)
    {
        using var insert = connection.CreateCommand();
        var columnList = string.Join(", ", table.Columns.Select(QuoteIdentifier));
        var parameterList = string.Join(", ", table.Columns.Select((_, index) => $"$p{index}"));
        insert.CommandText = $"INSERT INTO {QuoteIdentifier(table.Name)} ({columnList}) VALUES ({parameterList})";

        for (var index = 0; index < table.Columns.Count; index += 1)
        {
            var value = row.Values[table.Columns[index]];
            insert.Parameters.AddWithValue($"$p{index}", (object?)value ?? DBNull.Value);
        }

        insert.ExecuteNonQuery();
    }

    private static void WriteOutputDatabase(string outputPath, Action<SqliteConnection> write)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = outputPath + ".tmp";
        DeleteIfExists(tempPath);

        using (var connection = OpenWritableConnection(tempPath))
        {
            write(connection);
        }

        File.Move(tempPath, outputPath, overwrite: true);
    }

    private static SqliteConnection OpenReadOnlyConnection(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static SqliteConnection OpenWritableConnection(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static string QuoteIdentifier(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static DbMergeResult Failed(DbMergeRequest request, string message)
    {
        return new DbMergeResult(
            DbMergeStatus.Failed,
            null,
            new DbMergeReport(request.RecipeId, [], []),
            message);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record ConflictResolution(string? Value, ConflictEntry? ResolvedConflict, ConflictEntry? UnresolvedConflict);

    private sealed record KnownUserSnapshotRecord(string UserId, string UserName, string Email);

    private sealed record TableMerge(IReadOnlyList<DatabaseRow> Rows, TableMergeReport Report, IReadOnlyList<ConflictEntry> Conflicts);

    private sealed record TableSpec(string Name, IReadOnlyList<string> KeyColumns, IReadOnlyList<string> Columns);

    private sealed record DatabaseRow(string TableName, IReadOnlyDictionary<string, string?> Values)
    {
        public bool ContentEquals(DatabaseRow other)
        {
            return Values.Count == other.Values.Count
                && Values.All(value =>
                    other.Values.TryGetValue(value.Key, out var otherValue)
                    && string.Equals(value.Value, otherValue, StringComparison.Ordinal));
        }
    }

    private static readonly TableSpec[] RonFlowTableSpecs =
    [
        new("Projects", ["Id"], ["Id", "Data"]),
        new("Tasks", ["Id"], ["Id", "Data"]),
        new("PushSubscriptions", ["Endpoint"], ["Endpoint", "Data"]),
        new("WorkflowThroughputOutbox", ["MessageId"], ["MessageId", "ProjectId", "TaskId", "EventType", "StateKey", "OccurredAt", "ProcessedAt"]),
        new("WorkflowThroughputBuckets", ["ProjectId", "BucketType", "BucketStart"], ["ProjectId", "BucketType", "BucketStart", "CreatedCount", "MovedToActiveCount", "MovedToReviewCount", "CompletedCount", "ReopenedCount", "LastUpdatedAt"]),
        new("AiAuditOutbox", ["MessageId"], ["MessageId", "AuditEntryId", "SessionId", "ActorType", "ActorIdentity", "TargetType", "TargetId", "RequestedChange", "ResultStatus", "ActualDiffText", "OccurredAt", "ProcessedAt"]),
        new("AiAuditReadModel", ["AuditEntryId"], ["AuditEntryId", "SessionId", "ActorType", "ActorIdentity", "TargetType", "TargetId", "RequestedChange", "ResultStatus", "ActualDiffText", "OccurredAt", "ProjectedAt"]),
    ];

    private const string RonFlowSchemaSql = @"
CREATE TABLE IF NOT EXISTS Projects (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Tasks (
    Id TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PushSubscriptions (
    Endpoint TEXT NOT NULL PRIMARY KEY,
    Data TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS KnownUsers (
    UserId TEXT NOT NULL PRIMARY KEY,
    UserName TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS WorkflowThroughputOutbox (
    MessageId TEXT NOT NULL PRIMARY KEY,
    ProjectId TEXT NOT NULL,
    TaskId TEXT NOT NULL,
    EventType TEXT NOT NULL,
    StateKey TEXT NULL,
    OccurredAt TEXT NOT NULL,
    ProcessedAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS WorkflowThroughputBuckets (
    ProjectId TEXT NOT NULL,
    BucketType TEXT NOT NULL,
    BucketStart TEXT NOT NULL,
    CreatedCount INTEGER NOT NULL,
    MovedToActiveCount INTEGER NOT NULL,
    MovedToReviewCount INTEGER NOT NULL,
    CompletedCount INTEGER NOT NULL,
    ReopenedCount INTEGER NOT NULL,
    LastUpdatedAt TEXT NOT NULL,
    PRIMARY KEY (ProjectId, BucketType, BucketStart)
);

CREATE TABLE IF NOT EXISTS AiAuditOutbox (
    MessageId TEXT NOT NULL PRIMARY KEY,
    AuditEntryId TEXT NOT NULL,
    SessionId TEXT NOT NULL,
    ActorType TEXT NOT NULL,
    ActorIdentity TEXT NOT NULL,
    TargetType TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    RequestedChange TEXT NOT NULL,
    ResultStatus TEXT NOT NULL,
    ActualDiffText TEXT NOT NULL,
    OccurredAt TEXT NOT NULL,
    ProcessedAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS AiAuditReadModel (
    AuditEntryId TEXT NOT NULL PRIMARY KEY,
    SessionId TEXT NOT NULL,
    ActorType TEXT NOT NULL,
    ActorIdentity TEXT NOT NULL,
    TargetType TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    RequestedChange TEXT NOT NULL,
    ResultStatus TEXT NOT NULL,
    ActualDiffText TEXT NOT NULL,
    OccurredAt TEXT NOT NULL,
    ProjectedAt TEXT NOT NULL
);";
}
