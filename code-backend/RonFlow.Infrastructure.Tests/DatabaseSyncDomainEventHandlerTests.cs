using RonFlow.Domain;
using RonFlow.Infrastructure;

namespace RonFlow.Infrastructure.Tests;

public sealed class DatabaseSyncDomainEventHandlerTests
{
    [Test]
    public void Handle_WhenCoreFlowDataChanged_EnqueuesDatabaseSyncReason()
    {
        var coordinator = new CapturingDatabaseSyncCoordinator();
        var handler = new DatabaseSyncDomainEventHandler(coordinator, NoOpDatabaseSyncInitiatorContext.Instance);

        handler.Handle(new CoreFlowDataChangedDomainEvent("project updated"));

        Assert.That(coordinator.PushReasons, Is.EqualTo(new[] { "project updated" }));
        Assert.That(coordinator.InitiatorUserIds, Is.EqualTo(new Guid?[] { null }));
        Assert.That(coordinator.FlushCount, Is.EqualTo(0));
    }

    [Test]
    public void Handle_WhenInitiatorExists_PassesInitiatorToCoordinator()
    {
        var userId = Guid.NewGuid();
        var coordinator = new CapturingDatabaseSyncCoordinator();
        var handler = new DatabaseSyncDomainEventHandler(coordinator, new StaticInitiatorContext(new DatabaseSyncInitiator(userId)));

        handler.Handle(new CoreFlowDataChangedDomainEvent("task updated"));

        Assert.That(coordinator.PushReasons, Is.EqualTo(new[] { "task updated" }));
        Assert.That(coordinator.InitiatorUserIds, Is.EqualTo(new Guid?[] { userId }));
    }

    [Test]
    public void CanHandle_ReturnsTrueOnlyForCoreFlowDataChanged()
    {
        var handler = new DatabaseSyncDomainEventHandler(new CapturingDatabaseSyncCoordinator(), NoOpDatabaseSyncInitiatorContext.Instance);

        Assert.That(handler.CanHandle(new CoreFlowDataChangedDomainEvent("task updated")), Is.True);
        Assert.That(handler.CanHandle(new OtherDomainEvent()), Is.False);
    }

    private sealed class CapturingDatabaseSyncCoordinator : IDatabaseSyncCoordinator
    {
        public List<string> PushReasons { get; } = [];

        public List<Guid?> InitiatorUserIds { get; } = [];

        public int FlushCount { get; private set; }

        public void SynchronizeStartupSnapshot()
        {
        }

        public void RequestPullIfStale(string reason)
        {
        }

        public bool FlushPendingPullRequests()
        {
            return false;
        }

        public void PushAfterMutation(string reason)
        {
            PushReasons.Add(reason);
            InitiatorUserIds.Add(null);
        }

        public void PushAfterMutation(string reason, DatabaseSyncInitiator? initiator)
        {
            PushReasons.Add(reason);
            InitiatorUserIds.Add(initiator?.UserId);
        }

        public bool FlushPendingMutations()
        {
            FlushCount++;
            return false;
        }
    }

    private sealed class StaticInitiatorContext(DatabaseSyncInitiator? initiator) : IDatabaseSyncInitiatorContext
    {
        public DatabaseSyncInitiator? GetCurrent() => initiator;
    }

    private sealed record OtherDomainEvent(DateTimeOffset OccurredAt) : IDomainEvent
    {
        public OtherDomainEvent()
            : this(DateTimeOffset.UtcNow)
        {
        }
    }
}
