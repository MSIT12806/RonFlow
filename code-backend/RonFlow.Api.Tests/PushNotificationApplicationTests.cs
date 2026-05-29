using RonFlow.Application;
using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Api.Tests;

public sealed class RegisterPushSubscriptionCommandServiceTests
{
    [Test]
    public void Register_WithValidPayload_StoresSubscription()
    {
        var subscribedAt = new DateTimeOffset(2026, 5, 20, 8, 45, 0, TimeSpan.Zero);
        var repository = new TestPushSubscriptionRepository();
        var commandService = new RegisterPushSubscriptionCommandService(repository, new FixedTimeProvider(subscribedAt));

        var result = commandService.Register(
            "https://push.example.test/subscriptions/device-1",
            "p256dh-key",
            "auth-key");

        Assert.That(result.ValidationError, Is.Null);
        Assert.That(repository.GetAll(), Has.Count.EqualTo(1));

        var subscription = repository.GetAll().Single();

        Assert.That(subscription.Endpoint, Is.EqualTo("https://push.example.test/subscriptions/device-1"));
        Assert.That(subscription.P256dh, Is.EqualTo("p256dh-key"));
        Assert.That(subscription.Auth, Is.EqualTo("auth-key"));
        Assert.That(subscription.SubscribedAt, Is.EqualTo(subscribedAt));
    }

    [Test]
    public void Register_WithSameEndpoint_ReplacesExistingSubscriptionKeys()
    {
        var repository = new TestPushSubscriptionRepository();
        repository.Upsert(new PushSubscription(
            "https://push.example.test/subscriptions/device-1",
            "old-p256dh",
            "old-auth",
            new DateTimeOffset(2026, 5, 20, 8, 45, 0, TimeSpan.Zero)));

        var updatedAt = new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero);
        var commandService = new RegisterPushSubscriptionCommandService(repository, new FixedTimeProvider(updatedAt));

        var result = commandService.Register(
            "https://push.example.test/subscriptions/device-1",
            "new-p256dh",
            "new-auth");

        Assert.That(result.ValidationError, Is.Null);
        Assert.That(repository.GetAll(), Has.Count.EqualTo(1));

        var subscription = repository.GetAll().Single();

        Assert.That(subscription.P256dh, Is.EqualTo("new-p256dh"));
        Assert.That(subscription.Auth, Is.EqualTo("new-auth"));
        Assert.That(subscription.SubscribedAt, Is.EqualTo(updatedAt));
    }
}

public sealed class DeliverDueReminderNotificationsCommandServiceTests
{
    [Test]
    public void Deliver_WhenReminderIsDue_SendsPushAndMarksReminderAsDispatched()
    {
        var createdAt = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero);
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var subscriptionRepository = new TestPushSubscriptionRepository();
        var sender = new RecordingPushNotificationSender();
        var project = Project.Create(TestObjectFactory.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);

        var task = DomainTask.Create(
            project.Id,
            TestObjectFactory.CreateTaskTitle("Build Kanban Board"),
            project.GetDefaultWorkflowState(),
            createdAt,
            0);
        task.AddReminder(
            TaskMutationAuthorization.Granted(TaskMutationKind.CreateReminder),
            "2026-05-20T09:00",
            "提醒確認欄位狀態",
            createdAt.AddMinutes(5));
        taskRepository.Add(task);

        subscriptionRepository.Upsert(new PushSubscription(
            "https://push.example.test/subscriptions/device-1",
            "p256dh-key",
            "auth-key",
            createdAt.AddMinutes(1)));

        var commandService = new DeliverDueReminderNotificationsCommandService(
            taskRepository,
            subscriptionRepository,
            sender,
            new FixedTimeProvider(dueAt));

        commandService.DeliverDueReminders();

        Assert.That(sender.SentNotifications, Has.Count.EqualTo(1));

        var sentNotification = sender.SentNotifications.Single();
        Assert.That(sentNotification.Subscription.Endpoint, Is.EqualTo("https://push.example.test/subscriptions/device-1"));
        Assert.That(sentNotification.Payload.Title, Is.EqualTo("Build Kanban Board"));
        Assert.That(sentNotification.Payload.Body, Does.Contain("提醒確認欄位狀態"));

        var persistedTask = taskRepository.Get(task.Id);

        Assert.That(persistedTask, Is.Not.Null);
        Assert.That(persistedTask!.Reminders.Single().NotificationDispatchedAt, Is.EqualTo(dueAt));

        commandService.DeliverDueReminders();

        Assert.That(sender.SentNotifications, Has.Count.EqualTo(1));
    }

    [Test]
    public void Deliver_WhenPushSubscriptionIsExpired_RemovesSubscriptionAndStillMarksReminderAsDispatched()
    {
        var createdAt = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero);
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var subscriptionRepository = new TestPushSubscriptionRepository();
        var sender = new RecordingPushNotificationSender(
            expiredEndpoints: ["https://push.example.test/subscriptions/device-1"]);
        var project = Project.Create(TestObjectFactory.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);

        var task = DomainTask.Create(
            project.Id,
            TestObjectFactory.CreateTaskTitle("Build Kanban Board"),
            project.GetDefaultWorkflowState(),
            createdAt,
            0);
        task.AddReminder(
            TaskMutationAuthorization.Granted(TaskMutationKind.CreateReminder),
            "2026-05-20T09:00",
            string.Empty,
            createdAt.AddMinutes(5));
        taskRepository.Add(task);

        subscriptionRepository.Upsert(new PushSubscription(
            "https://push.example.test/subscriptions/device-1",
            "p256dh-key",
            "auth-key",
            createdAt.AddMinutes(1)));

        var commandService = new DeliverDueReminderNotificationsCommandService(
            taskRepository,
            subscriptionRepository,
            sender,
            new FixedTimeProvider(dueAt));

        commandService.DeliverDueReminders();

        Assert.That(subscriptionRepository.GetAll(), Is.Empty);

        var persistedTask = taskRepository.Get(task.Id);

        Assert.That(persistedTask, Is.Not.Null);
        Assert.That(persistedTask!.Reminders.Single().NotificationDispatchedAt, Is.EqualTo(dueAt));
    }
}

internal sealed class TestPushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly Dictionary<string, PushSubscription> subscriptions = new(StringComparer.Ordinal);

    public IReadOnlyList<PushSubscription> GetAll()
    {
        return subscriptions.Values
            .OrderBy(subscription => subscription.SubscribedAt)
            .ToArray();
    }

    public void Upsert(PushSubscription subscription)
    {
        subscriptions[subscription.Endpoint] = subscription;
    }

    public void Remove(string endpoint)
    {
        subscriptions.Remove(endpoint);
    }
}

internal sealed class RecordingPushNotificationSender(IReadOnlyCollection<string>? expiredEndpoints = null) : IPushNotificationSender
{
    private readonly HashSet<string> expiredEndpointSet = expiredEndpoints is null
        ? []
        : [.. expiredEndpoints];

    public List<PushNotificationDelivery> SentNotifications { get; } = [];

    public PushNotificationSendResult Send(PushSubscription subscription, PushNotificationPayload payload)
    {
        SentNotifications.Add(new PushNotificationDelivery(subscription, payload));

        return expiredEndpointSet.Contains(subscription.Endpoint)
            ? PushNotificationSendResult.ExpiredSubscription
            : PushNotificationSendResult.Success;
    }
}

internal sealed record PushNotificationDelivery(PushSubscription Subscription, PushNotificationPayload Payload);