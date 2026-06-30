using RonFlow.Domain;

namespace RonFlow.Testing.Infrastructure;

public sealed class InMemoryPushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly object syncRoot = new();
    private readonly Dictionary<string, PushSubscription> subscriptions = new(StringComparer.Ordinal);

    public IReadOnlyList<PushSubscription> GetAll()
    {
        lock (syncRoot)
        {
            return subscriptions.Values
                .OrderBy(subscription => subscription.SubscribedAt)
                .ToArray();
        }
    }

    public void Upsert(PushSubscription subscription)
    {
        lock (syncRoot)
        {
            subscriptions[subscription.Endpoint] = subscription;
        }
    }

    public void Remove(string endpoint)
    {
        lock (syncRoot)
        {
            subscriptions.Remove(endpoint);
        }
    }
}
