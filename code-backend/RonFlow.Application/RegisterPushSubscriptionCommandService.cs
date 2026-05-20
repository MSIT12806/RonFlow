using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class RegisterPushSubscriptionCommandService(
    IPushSubscriptionRepository pushSubscriptionRepository,
    TimeProvider timeProvider)
{
    public RegisterPushSubscriptionResult Register(string? endpoint, string? p256dh, string? auth)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return RegisterPushSubscriptionResult.Invalid("endpoint", "推播訂閱 endpoint 為必填欄位");
        }

        if (string.IsNullOrWhiteSpace(p256dh))
        {
            return RegisterPushSubscriptionResult.Invalid("keys.p256dh", "推播訂閱 p256dh 為必填欄位");
        }

        if (string.IsNullOrWhiteSpace(auth))
        {
            return RegisterPushSubscriptionResult.Invalid("keys.auth", "推播訂閱 auth 為必填欄位");
        }

        pushSubscriptionRepository.Upsert(new PushSubscription(
            endpoint.Trim(),
            p256dh.Trim(),
            auth.Trim(),
            timeProvider.GetUtcNow()));

        return RegisterPushSubscriptionResult.Success();
    }
}