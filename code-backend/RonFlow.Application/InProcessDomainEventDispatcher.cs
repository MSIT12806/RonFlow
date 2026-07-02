using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class InProcessDomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers) : IDomainEventDispatcher
{
    public void Dispatch(IDomainEvent domainEvent)
    {
        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(domainEvent))
            {
                continue;
            }

            handler.Handle(domainEvent);
        }
    }
}
