namespace RonFlow.Domain;

public sealed record ActivityTimelineItem(string Type, string Message, DateTimeOffset OccurredAt)
{
    public static ActivityTimelineItem TaskCreated(DateTimeOffset occurredAt)
    {
        return new("TaskCreated", "已建立任務", occurredAt);
    }

    public static ActivityTimelineItem TaskStateChanged(string stateLabel, DateTimeOffset occurredAt)
    {
        return new("TaskStateChanged", $"任務狀態已變更為 {stateLabel}", occurredAt);
    }

    public static ActivityTimelineItem TaskCompleted(DateTimeOffset occurredAt)
    {
        return new("TaskCompleted", "已完成任務", occurredAt);
    }

    public ActivityTimelineItemModel ToModel()
    {
        return new(Type, Message, OccurredAt);
    }
}