namespace RonFlow.Application;

public sealed class AiAuditRegistry(
    IAiAuditProjectionOutbox outbox,
    IAiAuditReadModelStore readModelStore,
    TimeProvider timeProvider)
{
    public Guid Record(
        string sessionId,
        string actorIdentity,
        string targetType,
        string targetId,
        string requestedChange,
        string resultStatus,
        IReadOnlyList<string> actualDiff)
    {
        var occurredAt = timeProvider.GetUtcNow();
        var auditEntryId = Guid.NewGuid();
        var source = new AiAuditProjectionSource(
            Guid.NewGuid(),
            auditEntryId,
            NormalizeSessionId(sessionId),
            "ai",
            actorIdentity,
            targetType,
            targetId,
            requestedChange,
            resultStatus,
            actualDiff.ToArray(),
            occurredAt,
            null);

        outbox.Enqueue(source);
        return auditEntryId;
    }

    public Guid Record(
        string actorIdentity,
        string targetType,
        string targetId,
        string requestedChange,
        string resultStatus,
        IReadOnlyList<string> actualDiff)
    {
        return Record("unknown", actorIdentity, targetType, targetId, requestedChange, resultStatus, actualDiff);
    }

    public AiAuditEntry? Get(Guid auditEntryId)
    {
        return readModelStore.Get(auditEntryId);
    }

    public IReadOnlyList<AiAuditEntry> Query(AiAuditQuery query)
    {
        var normalizedQuery = query with
        {
            SessionId = NormalizeNullable(query.SessionId),
            ActorIdentity = NormalizeNullable(query.ActorIdentity),
            TargetType = NormalizeNullable(query.TargetType),
            TargetId = NormalizeNullable(query.TargetId),
            RequestedChange = NormalizeNullable(query.RequestedChange),
            ActualDiffContains = NormalizeNullable(query.ActualDiffContains),
            Limit = Math.Clamp(query.Limit, 1, 200),
        };

        return readModelStore.Query(normalizedQuery);
    }

    private static string NormalizeSessionId(string sessionId)
    {
        return string.IsNullOrWhiteSpace(sessionId)
            ? "unknown"
            : sessionId.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}

public sealed record AiAuditEntry(
    Guid Id,
    string SessionId,
    string ActorType,
    string ActorIdentity,
    string TargetType,
    string TargetId,
    string RequestedChange,
    string ResultStatus,
    IReadOnlyList<string> ActualDiff,
    DateTimeOffset OccurredAt);