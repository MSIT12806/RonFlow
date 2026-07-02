using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class DatabaseSyncDomainEventHandler(IDatabaseSyncCoordinator databaseSyncCoordinator) : IDomainEventHandler
{
    public bool CanHandle(IDomainEvent domainEvent)
    {
        return domainEvent is CoreFlowDataChangedDomainEvent;
    }

    public void Handle(IDomainEvent domainEvent)
    {
        if (domainEvent is CoreFlowDataChangedDomainEvent coreFlowDataChanged)
        {
            databaseSyncCoordinator.PushAfterMutation(coreFlowDataChanged.Reason);
        }
    }
}
