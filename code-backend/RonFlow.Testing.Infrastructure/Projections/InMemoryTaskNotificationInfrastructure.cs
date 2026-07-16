using RonFlow.Application;
using RonFlow.Domain;

namespace RonFlow.Testing.Infrastructure;

public sealed class InMemoryTaskNotificationOutbox : ITaskNotificationOutbox
{
    private readonly object syncRoot = new();
    private readonly List<TaskNotificationSource> pending = [];

    public void EnqueueWorkflowStateChanged(TaskWorkflowStateChangedDomainEvent domainEvent)
    {
        Add(new TaskNotificationSource(Guid.NewGuid(), domainEvent.ActorUserId, domainEvent.ProjectId, domainEvent.TaskId, "TaskWorkflowStateChanged", domainEvent.TaskTitle, domainEvent.StateKey, domainEvent.StateLabel, domainEvent.OccurredAt, null));
    }

    public void EnqueueMovedToTrash(TaskMovedToTrashDomainEvent domainEvent)
    {
        Add(new TaskNotificationSource(Guid.NewGuid(), domainEvent.ActorUserId, domainEvent.ProjectId, domainEvent.TaskId, "TaskMovedToTrash", domainEvent.TaskTitle, null, null, domainEvent.OccurredAt, null));
    }

    public IReadOnlyList<TaskNotificationSource> GetPending()
    {
        lock (syncRoot)
        {
            return pending.Where(item => item.ProcessedAt is null).OrderBy(item => item.OccurredAt).ToArray();
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

    private void Add(TaskNotificationSource source)
    {
        lock (syncRoot)
        {
            pending.Add(source);
        }
    }
}
