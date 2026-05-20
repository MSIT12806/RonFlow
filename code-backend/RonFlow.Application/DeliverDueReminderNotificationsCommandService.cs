using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Application;

public sealed class DeliverDueReminderNotificationsCommandService(
    ITaskRepository taskRepository,
    IPushSubscriptionRepository pushSubscriptionRepository,
    IPushNotificationSender pushNotificationSender,
    TimeProvider timeProvider)
{
    public void DeliverDueReminders()
    {
        var currentTime = timeProvider.GetUtcNow();
        var subscriptions = pushSubscriptionRepository.GetAll();

        foreach (var task in taskRepository.GetAll())
        {
            var dueReminders = task.GetDueUndispatchedReminders(currentTime);
            if (dueReminders.Count == 0)
            {
                continue;
            }

            foreach (var reminder in dueReminders)
            {
                foreach (var subscription in subscriptions)
                {
                    var sendResult = pushNotificationSender.Send(
                        subscription,
                        CreatePayload(task, reminder));

                    if (sendResult == PushNotificationSendResult.ExpiredSubscription)
                    {
                        pushSubscriptionRepository.Remove(subscription.Endpoint);
                    }
                }

                task.MarkReminderNotificationDispatched(reminder.Id, currentTime);
            }

            taskRepository.Update(task);
        }
    }

    private static PushNotificationPayload CreatePayload(DomainTask task, TaskReminder reminder)
    {
        var body = string.IsNullOrWhiteSpace(reminder.Description)
            ? $"任務「{task.Title}」的提醒時間已到。"
            : $"任務「{task.Title}」提醒：{reminder.Description}";

        return new PushNotificationPayload(
            task.Title,
            body,
            "/",
            $"task-reminder-{reminder.Id}");
    }
}