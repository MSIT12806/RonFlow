using RonFlow.Domain;
using Microsoft.Extensions.Logging;

namespace RonFlow.Infrastructure;

public sealed class DatabaseSyncDomainEventHandler(
    IDatabaseSyncCoordinator databaseSyncCoordinator,
    ILogger<DatabaseSyncDomainEventHandler>? logger = null) : IDomainEventHandler
{
    public bool CanHandle(IDomainEvent domainEvent)
    {
        return domainEvent is CoreFlowDataChangedDomainEvent;
    }

    public void Handle(IDomainEvent domainEvent)
    {
        if (domainEvent is CoreFlowDataChangedDomainEvent coreFlowDataChanged)
        {
            try
            {
                databaseSyncCoordinator.PushAfterMutation(coreFlowDataChanged.Reason);
            }
            catch (Exception exception)
            {
                logger?.LogWarning(
                    exception,
                    "Failed to queue RonFlow database Git sync mutation from domain event. Reason: {Reason}; OccurredAt: {OccurredAt}",
                    coreFlowDataChanged.Reason,
                    coreFlowDataChanged.OccurredAt);
            }
        }
    }
}
