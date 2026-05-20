namespace RonFlow.Domain;

public interface IPushSubscriptionRepository
{
    IReadOnlyList<PushSubscription> GetAll();

    void Upsert(PushSubscription subscription);

    void Remove(string endpoint);
}