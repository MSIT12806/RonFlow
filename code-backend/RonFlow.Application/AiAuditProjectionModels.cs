namespace RonFlow.Application;

public sealed record AiAuditProjectionSource(
    Guid MessageId,
    Guid AuditEntryId,
    string SessionId,
    string ActorType,
    string ActorIdentity,
    string TargetType,
    string TargetId,
    string RequestedChange,
    string ResultStatus,
    IReadOnlyList<string> ActualDiff,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ProcessedAt);

public sealed record AiAuditQuery(
    string? SessionId,
    string? ActorIdentity,
    string? TargetType,
    string? TargetId,
    string? RequestedChange,
    string? ActualDiffContains,
    int Limit = 20);

public interface IAiAuditProjectionOutbox
{
    void Enqueue(AiAuditProjectionSource source);

    IReadOnlyList<AiAuditProjectionSource> GetPending(int maxCount);

    void MarkProcessed(Guid messageId, DateTimeOffset processedAt);
}

public interface IAiAuditReadModelStore
{
    void Upsert(AiAuditEntry entry, DateTimeOffset projectedAt);

    AiAuditEntry? Get(Guid auditEntryId);

    IReadOnlyList<AiAuditEntry> Query(AiAuditQuery query);
}

public sealed class ProcessAiAuditProjectionService(
    IAiAuditProjectionOutbox outbox,
    IAiAuditReadModelStore readModelStore,
    TimeProvider timeProvider)
{
    public void ProcessPending(int maxCount = 200)
    {
        foreach (var source in outbox.GetPending(maxCount))
        {
            var processedAt = timeProvider.GetUtcNow();
            var projectedEntry = new AiAuditEntry(
                source.AuditEntryId,
                source.SessionId,
                source.ActorType,
                source.ActorIdentity,
                source.TargetType,
                source.TargetId,
                source.RequestedChange,
                source.ResultStatus,
                source.ActualDiff,
                source.OccurredAt);

            readModelStore.Upsert(projectedEntry, processedAt);
            outbox.MarkProcessed(source.MessageId, processedAt);
        }
    }
}