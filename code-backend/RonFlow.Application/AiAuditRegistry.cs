using System.Collections.Concurrent;

namespace RonFlow.Application;

public sealed class AiAuditRegistry
{
    private readonly ConcurrentDictionary<Guid, AiAuditEntry> entries = new();

    public Guid Record(
        string actorIdentity,
        string targetType,
        string targetId,
        string requestedChange,
        string resultStatus,
        IReadOnlyList<string> actualDiff)
    {
        var auditEntry = new AiAuditEntry(
            Guid.NewGuid(),
            "ai",
            actorIdentity,
            targetType,
            targetId,
            requestedChange,
            resultStatus,
            actualDiff);

        entries[auditEntry.Id] = auditEntry;
        return auditEntry.Id;
    }

    public AiAuditEntry? Get(Guid auditEntryId)
    {
        return entries.TryGetValue(auditEntryId, out var auditEntry)
            ? auditEntry
            : null;
    }
}

public sealed record AiAuditEntry(
    Guid Id,
    string ActorType,
    string ActorIdentity,
    string TargetType,
    string TargetId,
    string RequestedChange,
    string ResultStatus,
    IReadOnlyList<string> ActualDiff);