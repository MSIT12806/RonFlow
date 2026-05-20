namespace RonFlow.Domain;

public sealed record TaskReminder(Guid Id, string ReminderDateTime, string Description)
{
    public TaskReminderModel ToModel()
    {
        return new(Id, ReminderDateTime, Description);
    }
}