using RonFlow.Application;
using RonFlow.Testing.Infrastructure;

namespace RonFlow.Api.Tests;

public sealed class AiAuditProjectionTests
{
    [Test]
    public void ProcessPending_WhenSameAuditEntryIsReplayed_UpsertsToSingleReadModelEntry()
    {
        var outbox = new InMemoryAiAuditProjectionOutbox();
        var readModelStore = new InMemoryAiAuditReadModelStore();
        var projectionService = new ProcessAiAuditProjectionService(
            outbox,
            readModelStore,
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero)));

        var auditEntryId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 6, 4, 11, 55, 0, TimeSpan.Zero);
        outbox.Enqueue(new AiAuditProjectionSource(
            Guid.NewGuid(),
            auditEntryId,
            "session-a",
            "ai",
            "copilot-a",
            "task",
            "task-1",
            "update_task_detail",
            "success",
            ["title: old -> new"],
            occurredAt,
            null));
        outbox.Enqueue(new AiAuditProjectionSource(
            Guid.NewGuid(),
            auditEntryId,
            "session-a",
            "ai",
            "copilot-a",
            "task",
            "task-1",
            "update_task_detail",
            "success",
            ["title: old -> new"],
            occurredAt,
            null));

        projectionService.ProcessPending();

        var queried = readModelStore.Query(new AiAuditQuery(
            SessionId: "session-a",
            ActorIdentity: null,
            TargetType: null,
            TargetId: null,
            RequestedChange: "update_task_detail",
            ActualDiffContains: null,
            Limit: 20));

        Assert.That(queried.Count, Is.EqualTo(1));
        Assert.That(queried[0].Id, Is.EqualTo(auditEntryId));
    }

    [Test]
    public void Query_WhenCombinedFiltersProvided_ReturnsOnlyMatchedEntries()
    {
        var outbox = new InMemoryAiAuditProjectionOutbox();
        var readModelStore = new InMemoryAiAuditReadModelStore();
        var projectionService = new ProcessAiAuditProjectionService(
            outbox,
            readModelStore,
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 12, 30, 0, TimeSpan.Zero)));

        outbox.Enqueue(new AiAuditProjectionSource(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "session-target",
            "ai",
            "copilot-a",
            "task",
            "task-100",
            "move_task_state",
            "success",
            ["workflow_state_key: todo -> active"],
            new DateTimeOffset(2026, 6, 4, 12, 10, 0, TimeSpan.Zero),
            null));
        outbox.Enqueue(new AiAuditProjectionSource(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "session-other",
            "ai",
            "copilot-b",
            "task",
            "task-101",
            "update_task_detail",
            "success",
            ["title: a -> b"],
            new DateTimeOffset(2026, 6, 4, 12, 11, 0, TimeSpan.Zero),
            null));

        projectionService.ProcessPending();

        var queried = readModelStore.Query(new AiAuditQuery(
            SessionId: "session-target",
            ActorIdentity: "copilot-a",
            TargetType: "task",
            TargetId: "task-100",
            RequestedChange: "move_task_state",
            ActualDiffContains: "todo -> active",
            Limit: 20));

        Assert.That(queried.Count, Is.EqualTo(1));
        Assert.That(queried[0].SessionId, Is.EqualTo("session-target"));
        Assert.That(queried[0].RequestedChange, Is.EqualTo("move_task_state"));
    }
}
