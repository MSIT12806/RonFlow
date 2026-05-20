namespace RonFlow.Application;

public sealed record PushNotificationPayload(string Title, string Body, string Url, string Tag);

public enum PushNotificationSendResult
{
    Success,
    ExpiredSubscription,
}