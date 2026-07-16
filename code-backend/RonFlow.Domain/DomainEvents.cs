namespace RonFlow.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public interface IDomainEventDispatcher
{
    void Dispatch(IDomainEvent domainEvent);
}

public interface IDomainEventHandler
{
    bool CanHandle(IDomainEvent domainEvent);

    void Handle(IDomainEvent domainEvent);
}

public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public static NoOpDomainEventDispatcher Instance { get; } = new();

    private NoOpDomainEventDispatcher()
    {
    }

    public void Dispatch(IDomainEvent domainEvent)
    {
    }
}

public sealed record CoreFlowDataChangedDomainEvent(string Reason, DateTimeOffset OccurredAt) : IDomainEvent
{
    public CoreFlowDataChangedDomainEvent(string reason)
        : this(NormalizeReason(reason), DateTimeOffset.UtcNow)
    {
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "unspecified mutation"
            : reason.Trim();
    }
}

public sealed record TaskWorkflowStateChangedDomainEvent(
    Guid ActorUserId,
    Guid ProjectId,
    Guid TaskId,
    string TaskTitle,
    string StateKey,
    string StateLabel,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TaskMovedToTrashDomainEvent(
    Guid ActorUserId,
    Guid ProjectId,
    Guid TaskId,
    string TaskTitle,
    DateTimeOffset OccurredAt) : IDomainEvent;
