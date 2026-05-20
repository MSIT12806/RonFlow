using System.Globalization;

namespace RonFlow.Domain;

public sealed record TaskReminder(Guid Id, string ReminderDateTime, string Description, DateTimeOffset? NotificationDispatchedAt = null)
{
    public TaskReminderModel ToModel()
    {
        return new(Id, ReminderDateTime, Description);
    }

    public bool IsDue(DateTimeOffset currentTime)
    {
        if (NotificationDispatchedAt is not null)
        {
            return false;
        }

        if (!DateTime.TryParse(ReminderDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var reminderLocalDateTime))
        {
            return false;
        }

        return reminderLocalDateTime <= currentTime.LocalDateTime;
    }

    public TaskReminder MarkNotificationDispatched(DateTimeOffset dispatchedAt)
    {
        return this with { NotificationDispatchedAt = dispatchedAt };
    }
}