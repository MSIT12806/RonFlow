using Microsoft.Data.Sqlite;
using RonFlow.Application;

namespace RonFlow.Infrastructure;

public sealed class InMemoryAiAuditProjectionOutbox : IAiAuditProjectionOutbox
{
    private readonly object syncRoot = new();
    private readonly List<AiAuditProjectionSource> pending = [];

    public void Enqueue(AiAuditProjectionSource source)
    {
        lock (syncRoot)
        {
            pending.Add(source);
        }
    }

    public IReadOnlyList<AiAuditProjectionSource> GetPending(int maxCount)
    {
        lock (syncRoot)
        {
            return pending
                .Where(item => item.ProcessedAt is null)
                .OrderBy(item => item.OccurredAt)
                .Take(Math.Max(1, maxCount))
                .ToArray();
        }
    }

    public void MarkProcessed(Guid messageId, DateTimeOffset processedAt)
    {
        lock (syncRoot)
        {
            var index = pending.FindIndex(item => item.MessageId == messageId);
            if (index >= 0)
            {
                pending[index] = pending[index] with { ProcessedAt = processedAt };
            }
        }
    }
}

public sealed class InMemoryAiAuditReadModelStore : IAiAuditReadModelStore
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, AiAuditEntry> entries = [];

    public void Upsert(AiAuditEntry entry, DateTimeOffset projectedAt)
    {
        lock (syncRoot)
        {
            entries[entry.Id] = entry;
        }
    }

    public AiAuditEntry? Get(Guid auditEntryId)
    {
        lock (syncRoot)
        {
            return entries.GetValueOrDefault(auditEntryId);
        }
    }

    public IReadOnlyList<AiAuditEntry> Query(AiAuditQuery query)
    {
        lock (syncRoot)
        {
            return entries.Values
                .Where(entry => Matches(query, entry))
                .OrderByDescending(entry => entry.OccurredAt)
                .Take(Math.Max(1, query.Limit))
                .ToArray();
        }
    }

    private static bool Matches(AiAuditQuery query, AiAuditEntry entry)
    {
        if (!MatchesExact(query.SessionId, entry.SessionId))
        {
            return false;
        }

        if (!MatchesExact(query.ActorIdentity, entry.ActorIdentity))
        {
            return false;
        }

        if (!MatchesExact(query.TargetType, entry.TargetType))
        {
            return false;
        }

        if (!MatchesExact(query.TargetId, entry.TargetId))
        {
            return false;
        }

        if (!MatchesExact(query.RequestedChange, entry.RequestedChange))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.ActualDiffContains))
        {
            var containsDiff = entry.ActualDiff.Any(diff =>
                diff.Contains(query.ActualDiffContains, StringComparison.OrdinalIgnoreCase));

            if (!containsDiff)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesExact(string? expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SqliteAiAuditProjectionOutbox(SqliteCoreFlowStore store) : IAiAuditProjectionOutbox
{
    public void Enqueue(AiAuditProjectionSource source)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO AiAuditOutbox (
    MessageId,
    AuditEntryId,
    SessionId,
    ActorType,
    ActorIdentity,
    TargetType,
    TargetId,
    RequestedChange,
    ResultStatus,
    ActualDiffText,
    OccurredAt,
    ProcessedAt)
VALUES (
    $messageId,
    $auditEntryId,
    $sessionId,
    $actorType,
    $actorIdentity,
    $targetType,
    $targetId,
    $requestedChange,
    $resultStatus,
    $actualDiffText,
    $occurredAt,
    NULL)";
        command.Parameters.AddWithValue("$messageId", source.MessageId.ToString());
        command.Parameters.AddWithValue("$auditEntryId", source.AuditEntryId.ToString());
        command.Parameters.AddWithValue("$sessionId", source.SessionId);
        command.Parameters.AddWithValue("$actorType", source.ActorType);
        command.Parameters.AddWithValue("$actorIdentity", source.ActorIdentity);
        command.Parameters.AddWithValue("$targetType", source.TargetType);
        command.Parameters.AddWithValue("$targetId", source.TargetId);
        command.Parameters.AddWithValue("$requestedChange", source.RequestedChange);
        command.Parameters.AddWithValue("$resultStatus", source.ResultStatus);
        command.Parameters.AddWithValue("$actualDiffText", JoinDiff(source.ActualDiff));
        command.Parameters.AddWithValue("$occurredAt", source.OccurredAt.ToString("O"));
        if (command.ExecuteNonQuery() > 0)
        {
            store.NotifyChanged("ai audit outbox enqueued");
        }
    }

    public IReadOnlyList<AiAuditProjectionSource> GetPending(int maxCount)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT MessageId,
       AuditEntryId,
       SessionId,
       ActorType,
       ActorIdentity,
       TargetType,
       TargetId,
       RequestedChange,
       ResultStatus,
       ActualDiffText,
       OccurredAt,
       ProcessedAt
FROM AiAuditOutbox
WHERE ProcessedAt IS NULL
ORDER BY OccurredAt
LIMIT $limit";
        command.Parameters.AddWithValue("$limit", Math.Max(1, maxCount));

        using var reader = command.ExecuteReader();
        var items = new List<AiAuditProjectionSource>();
        while (reader.Read())
        {
            items.Add(new AiAuditProjectionSource(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                SplitDiff(reader.GetString(9)),
                DateTimeOffset.Parse(reader.GetString(10)),
                reader.IsDBNull(11) ? null : DateTimeOffset.Parse(reader.GetString(11))));
        }

        return items;
    }

    public void MarkProcessed(Guid messageId, DateTimeOffset processedAt)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE AiAuditOutbox SET ProcessedAt = $processedAt WHERE MessageId = $messageId";
        command.Parameters.AddWithValue("$processedAt", processedAt.ToString("O"));
        command.Parameters.AddWithValue("$messageId", messageId.ToString());
        if (command.ExecuteNonQuery() > 0)
        {
            store.NotifyChanged("ai audit outbox processed");
        }
    }

    private static string JoinDiff(IReadOnlyList<string> diff)
    {
        return string.Join('\n', diff);
    }

    private static IReadOnlyList<string> SplitDiff(string rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return [];
        }

        return rawValue
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}

public sealed class SqliteAiAuditReadModelStore(SqliteCoreFlowStore store) : IAiAuditReadModelStore
{
    public void Upsert(AiAuditEntry entry, DateTimeOffset projectedAt)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO AiAuditReadModel (
    AuditEntryId,
    SessionId,
    ActorType,
    ActorIdentity,
    TargetType,
    TargetId,
    RequestedChange,
    ResultStatus,
    ActualDiffText,
    OccurredAt,
    ProjectedAt)
VALUES (
    $auditEntryId,
    $sessionId,
    $actorType,
    $actorIdentity,
    $targetType,
    $targetId,
    $requestedChange,
    $resultStatus,
    $actualDiffText,
    $occurredAt,
    $projectedAt)
ON CONFLICT(AuditEntryId) DO UPDATE SET
    SessionId = excluded.SessionId,
    ActorType = excluded.ActorType,
    ActorIdentity = excluded.ActorIdentity,
    TargetType = excluded.TargetType,
    TargetId = excluded.TargetId,
    RequestedChange = excluded.RequestedChange,
    ResultStatus = excluded.ResultStatus,
    ActualDiffText = excluded.ActualDiffText,
    OccurredAt = excluded.OccurredAt,
    ProjectedAt = excluded.ProjectedAt";
        command.Parameters.AddWithValue("$auditEntryId", entry.Id.ToString());
        command.Parameters.AddWithValue("$sessionId", entry.SessionId);
        command.Parameters.AddWithValue("$actorType", entry.ActorType);
        command.Parameters.AddWithValue("$actorIdentity", entry.ActorIdentity);
        command.Parameters.AddWithValue("$targetType", entry.TargetType);
        command.Parameters.AddWithValue("$targetId", entry.TargetId);
        command.Parameters.AddWithValue("$requestedChange", entry.RequestedChange);
        command.Parameters.AddWithValue("$resultStatus", entry.ResultStatus);
        command.Parameters.AddWithValue("$actualDiffText", string.Join('\n', entry.ActualDiff));
        command.Parameters.AddWithValue("$occurredAt", entry.OccurredAt.ToString("O"));
        command.Parameters.AddWithValue("$projectedAt", projectedAt.ToString("O"));
        if (command.ExecuteNonQuery() > 0)
        {
            store.NotifyChanged("ai audit read model upserted");
        }
    }

    public AiAuditEntry? Get(Guid auditEntryId)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT AuditEntryId,
       SessionId,
       ActorType,
       ActorIdentity,
       TargetType,
       TargetId,
       RequestedChange,
       ResultStatus,
       ActualDiffText,
       OccurredAt
FROM AiAuditReadModel
WHERE AuditEntryId = $auditEntryId";
        command.Parameters.AddWithValue("$auditEntryId", auditEntryId.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new AiAuditEntry(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            SplitDiff(reader.GetString(8)),
            DateTimeOffset.Parse(reader.GetString(9)));
    }

    public IReadOnlyList<AiAuditEntry> Query(AiAuditQuery query)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();

        var whereClauses = new List<string>();
        AppendExactFilter(whereClauses, command, "SessionId", "$sessionId", query.SessionId);
        AppendExactFilter(whereClauses, command, "ActorIdentity", "$actorIdentity", query.ActorIdentity);
        AppendExactFilter(whereClauses, command, "TargetType", "$targetType", query.TargetType);
        AppendExactFilter(whereClauses, command, "TargetId", "$targetId", query.TargetId);
        AppendExactFilter(whereClauses, command, "RequestedChange", "$requestedChange", query.RequestedChange);

        if (!string.IsNullOrWhiteSpace(query.ActualDiffContains))
        {
            whereClauses.Add("ActualDiffText LIKE $actualDiffContains");
            command.Parameters.AddWithValue("$actualDiffContains", $"%{query.ActualDiffContains}%");
        }

        var whereClause = whereClauses.Count == 0
            ? string.Empty
            : $"WHERE {string.Join(" AND ", whereClauses)}";

        command.CommandText = $@"
SELECT AuditEntryId,
       SessionId,
       ActorType,
       ActorIdentity,
       TargetType,
       TargetId,
       RequestedChange,
       ResultStatus,
       ActualDiffText,
       OccurredAt
FROM AiAuditReadModel
{whereClause}
ORDER BY OccurredAt DESC
LIMIT $limit";
        command.Parameters.AddWithValue("$limit", Math.Max(1, query.Limit));

        using var reader = command.ExecuteReader();
        var items = new List<AiAuditEntry>();
        while (reader.Read())
        {
            items.Add(new AiAuditEntry(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                SplitDiff(reader.GetString(8)),
                DateTimeOffset.Parse(reader.GetString(9))));
        }

        return items;
    }

    private static void AppendExactFilter(
        ICollection<string> whereClauses,
        SqliteCommand command,
        string column,
        string parameterName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        whereClauses.Add($"{column} = {parameterName}");
        command.Parameters.AddWithValue(parameterName, value);
    }

    private static IReadOnlyList<string> SplitDiff(string rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return [];
        }

        return rawValue
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
