using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record TaskNotificationSource(
    Guid MessageId,
    Guid RecipientUserId,
    Guid ProjectId,
    Guid TaskId,
    string EventType,
    string TaskTitle,
    string? StateKey,
    string? StateLabel,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ProcessedAt);

public interface ITaskNotificationOutbox
{
    void EnqueueWorkflowStateChanged(TaskWorkflowStateChangedDomainEvent domainEvent);

    void EnqueueMovedToTrash(TaskMovedToTrashDomainEvent domainEvent);

    IReadOnlyList<TaskNotificationSource> GetPending();

    void MarkProcessed(Guid messageId, DateTimeOffset processedAt);
}

public interface ITaskNotificationPublisher
{
    bool Publish(TaskNotificationSource notification);
}

public sealed class NoOpTaskNotificationOutbox : ITaskNotificationOutbox
{
    public void EnqueueWorkflowStateChanged(TaskWorkflowStateChangedDomainEvent domainEvent)
    {
    }

    public void EnqueueMovedToTrash(TaskMovedToTrashDomainEvent domainEvent)
    {
    }

    public IReadOnlyList<TaskNotificationSource> GetPending()
    {
        return [];
    }

    public void MarkProcessed(Guid messageId, DateTimeOffset processedAt)
    {
    }
}

public sealed class NoOpTaskNotificationPublisher : ITaskNotificationPublisher
{
    public static NoOpTaskNotificationPublisher Instance { get; } = new();

    private NoOpTaskNotificationPublisher()
    {
    }

    public bool Publish(TaskNotificationSource notification)
    {
        return true;
    }
}

public sealed class TaskNotificationDomainEventHandler(ITaskNotificationOutbox outbox) : IDomainEventHandler
{
    public bool CanHandle(IDomainEvent domainEvent)
    {
        return domainEvent is TaskWorkflowStateChangedDomainEvent or TaskMovedToTrashDomainEvent;
    }

    public void Handle(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case TaskWorkflowStateChangedDomainEvent workflowStateChanged:
                outbox.EnqueueWorkflowStateChanged(workflowStateChanged);
                break;
            case TaskMovedToTrashDomainEvent movedToTrash:
                outbox.EnqueueMovedToTrash(movedToTrash);
                break;
        }
    }
}

public sealed class ProcessTaskNotificationsService(
    ITaskNotificationOutbox outbox,
    ITaskNotificationPublisher publisher,
    TimeProvider timeProvider)
{
    public void ProcessPending()
    {
        foreach (var notification in outbox.GetPending())
        {
            if (publisher.Publish(notification))
            {
                outbox.MarkProcessed(notification.MessageId, timeProvider.GetUtcNow());
            }
        }
    }
}
