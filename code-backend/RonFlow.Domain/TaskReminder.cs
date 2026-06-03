using System.Globalization;

namespace RonFlow.Domain;

/// <summary>
/// 表示任務上的單一提醒。
/// </summary>
public sealed record TaskReminder(Guid Id, string ReminderDateTime, string Description, DateTimeOffset? NotificationDispatchedAt = null)
{
    /// <summary>
    /// 將提醒轉成對外輸出的 reminder model。
    /// </summary>
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