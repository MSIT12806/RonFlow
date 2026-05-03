namespace RonFlow.Domain;

public sealed record ActivityTimelineItem(string Type, string Message, DateTimeOffset OccurredAt)
{
    public static ActivityTimelineItem TaskCreated(DateTimeOffset occurredAt)
    {
        return new("TaskCreated", "已建立任務", occurredAt);
    }

    public ActivityTimelineItemModel ToModel()
    {
        return new(Type, Message, OccurredAt);
    }
}