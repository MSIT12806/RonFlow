using RonFlow.Application;

namespace RonFlow.Testing.Infrastructure;

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
