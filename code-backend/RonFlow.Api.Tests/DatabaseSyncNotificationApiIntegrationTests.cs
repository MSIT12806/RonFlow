using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;
using RonFlow.Infrastructure;
using SystemTask = System.Threading.Tasks.Task;

namespace RonFlow.Api.Tests;

public sealed class DatabaseSyncNotificationApiIntegrationTests : ApiIntegrationTestBase
{
    [Test]
    public async SystemTask GetDatabaseSyncNotifications_ReturnsOnlyCurrentUsersOperations()
    {
        var operationStore = GetRequiredService<IDatabaseSyncOperationStore>();
        var ownerOperation = operationStore.Create(TestUser.OwnerA.UserId, "owner task updated", DateTimeOffset.UtcNow.AddMinutes(-2));
        var otherOperation = operationStore.Create(TestUser.OwnerB.UserId, "other task updated", DateTimeOffset.UtcNow.AddMinutes(-1));
        operationStore.MarkCompleted([ownerOperation.Id, otherOperation.Id], succeeded: true, completedAt: DateTimeOffset.UtcNow, failureSummary: null);

        var response = await Client.GetAsync("/api/notifications/database-sync");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<DatabaseSyncOperationListResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Items.Select(item => item.Id), Is.EqualTo(new[] { ownerOperation.Id }));
        Assert.That(payload.Items.Single().Status, Is.EqualTo("succeeded"));
        Assert.That(payload.Items.Single().Reason, Is.EqualTo("owner task updated"));
    }
}
