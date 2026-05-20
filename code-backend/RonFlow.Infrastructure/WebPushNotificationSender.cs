using System.Net;
using System.Text.Json;
using RonFlow.Application;
using RonFlow.Domain;
using WebPush;
using DomainPushSubscription = RonFlow.Domain.PushSubscription;

namespace RonFlow.Infrastructure;

public sealed class WebPushNotificationSender(PushNotificationConfiguration configuration) : IPushNotificationSender
{
    private readonly WebPushClient client = new();
    private readonly VapidDetails vapidDetails = new(configuration.Subject, configuration.PublicKey, configuration.PrivateKey);

    public PushNotificationSendResult Send(DomainPushSubscription subscription, PushNotificationPayload payload)
    {
        var webPushSubscription = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var body = JsonSerializer.Serialize(new
        {
            title = payload.Title,
            body = payload.Body,
            url = payload.Url,
            tag = payload.Tag,
        });

        try
        {
            client.SendNotificationAsync(webPushSubscription, body, vapidDetails).GetAwaiter().GetResult();
            return PushNotificationSendResult.Success;
        }
        catch (WebPushException exception) when (exception.StatusCode == HttpStatusCode.Gone || exception.StatusCode == HttpStatusCode.NotFound)
        {
            return PushNotificationSendResult.ExpiredSubscription;
        }
    }
}