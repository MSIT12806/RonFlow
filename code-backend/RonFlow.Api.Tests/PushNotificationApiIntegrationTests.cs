using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;
using RonFlow.Domain;
using SystemTask = System.Threading.Tasks.Task;

namespace RonFlow.Api.Tests;

public sealed class PushNotificationApiIntegrationTests : ApiIntegrationTestBase
{
    [Test]
    public async SystemTask GetPublicKey_ReturnsConfiguredPublicKey()
    {
        var response = await Client.GetAsync("/api/notifications/push/public-key");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<PushNotificationPublicKeyResponse>();

        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.PublicKey, Is.Not.Empty);
    }

    [Test]
    public async SystemTask RegisterSubscription_WithBlankEndpoint_ReturnsValidationError()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/notifications/push/subscriptions",
            new RegisterPushSubscriptionRequest(
                string.Empty,
                new PushSubscriptionKeysRequest("p256dh-key", "auth-key")));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(response);

        Assert.That(errors, Does.ContainKey("endpoint"));
        Assert.That(errors["endpoint"], Does.Contain("推播訂閱 endpoint 為必填欄位"));
    }

    [Test]
    public async SystemTask RegisterSubscription_WithValidPayload_PersistsSubscription()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/notifications/push/subscriptions",
            new RegisterPushSubscriptionRequest(
                "https://push.example.test/subscriptions/device-1",
                new PushSubscriptionKeysRequest("p256dh-key", "auth-key")));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var repository = GetRequiredService<IPushSubscriptionRepository>();
        var subscription = repository.GetAll().Single();

        Assert.That(repository.GetAll(), Has.Count.EqualTo(1));
        Assert.That(subscription.Endpoint, Is.EqualTo("https://push.example.test/subscriptions/device-1"));
        Assert.That(subscription.P256dh, Is.EqualTo("p256dh-key"));
        Assert.That(subscription.Auth, Is.EqualTo("auth-key"));
    }
}