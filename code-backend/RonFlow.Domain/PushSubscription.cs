namespace RonFlow.Domain;

public sealed record PushSubscription(
    string Endpoint,
    string P256dh,
    string Auth,
    DateTimeOffset SubscribedAt);