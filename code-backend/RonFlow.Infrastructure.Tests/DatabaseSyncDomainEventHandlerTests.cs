using RonFlow.Domain;
using RonFlow.Infrastructure;

namespace RonFlow.Infrastructure.Tests;

public sealed class DatabaseSyncDomainEventHandlerTests
{
    [Test]
    public void Handle_WhenCoreFlowDataChanged_EnqueuesDatabaseSyncReason()
    {
        var coordinator = new CapturingDatabaseSyncCoordinator();
        var handler = new DatabaseSyncDomainEventHandler(coordinator);

        handler.Handle(new CoreFlowDataChangedDomainEvent("project updated"));

        Assert.That(coordinator.PushReasons, Is.EqualTo(new[] { "project updated" }));
        Assert.That(coordinator.FlushCount, Is.EqualTo(0));
    }

    [Test]
    public void CanHandle_ReturnsTrueOnlyForCoreFlowDataChanged()
    {
        var handler = new DatabaseSyncDomainEventHandler(new CapturingDatabaseSyncCoordinator());

        Assert.That(handler.CanHandle(new CoreFlowDataChangedDomainEvent("task updated")), Is.True);
        Assert.That(handler.CanHandle(new OtherDomainEvent()), Is.False);
    }

    private sealed class CapturingDatabaseSyncCoordinator : IDatabaseSyncCoordinator
    {
        public List<string> PushReasons { get; } = [];

        public int FlushCount { get; private set; }

        public void PullBeforeOpen()
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
        }

        public bool FlushPendingMutations()
        {
            FlushCount++;
            return false;
        }
    }

    private sealed record OtherDomainEvent(DateTimeOffset OccurredAt) : IDomainEvent
    {
        public OtherDomainEvent()
            : this(DateTimeOffset.UtcNow)
        {
        }
    }
}
