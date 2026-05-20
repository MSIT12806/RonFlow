using RonFlow.Domain;

namespace RonFlow.Application;

public interface IPushNotificationSender
{
    PushNotificationSendResult Send(PushSubscription subscription, PushNotificationPayload payload);
}