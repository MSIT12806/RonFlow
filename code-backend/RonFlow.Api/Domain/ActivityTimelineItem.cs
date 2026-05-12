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

    public static ActivityTimelineItem TaskTitleChanged(DateTimeOffset occurredAt)
    {
        return new("TaskTitleChanged", "已更新任務標題", occurredAt);
    }

    public static ActivityTimelineItem TaskDescriptionChanged(DateTimeOffset occurredAt)
    {
        return new("TaskDescriptionChanged", "已更新任務描述", occurredAt);
    }

    public static ActivityTimelineItem TaskDueDateChanged(DateOnly? dueDate, DateTimeOffset occurredAt)
    {
        var message = dueDate is null
            ? "已清除到期日"
            : $"已設定到期日為 {dueDate.Value:yyyy/MM/dd}";

        return new("TaskDueDateChanged", message, occurredAt);
    }

    public static ActivityTimelineItem TaskCompleted(DateTimeOffset occurredAt)
    {
        return new("TaskCompleted", "已完成任務", occurredAt);
    }

    public static ActivityTimelineItem TaskReopened(DateTimeOffset occurredAt)
    {
        return new("TaskReopened", "已重新開啟任務", occurredAt);
    }

    public static ActivityTimelineItem TaskReordered(DateTimeOffset occurredAt)
    {
        return new("TaskReordered", "已調整任務順序", occurredAt);
    }

    public ActivityTimelineItemModel ToModel()
    {
        return new(Type, Message, OccurredAt);
    }
}