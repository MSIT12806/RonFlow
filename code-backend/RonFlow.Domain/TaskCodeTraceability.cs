namespace RonFlow.Domain;

public sealed record TaskCodeTraceabilityItem(string ChangeType, string Target)
{
    /// <summary>
    /// 將單一程式碼追蹤項目轉成對外輸出的 traceability item model。
    /// </summary>
    public TaskCodeTraceabilityItemModel ToModel()
    {
        return new(ChangeType, Target);
    }
}

public sealed record TaskCodeTraceability(
    IReadOnlyList<TaskCodeTraceabilityItem> Api,
    IReadOnlyList<TaskCodeTraceabilityItem> FrontendPages,
    IReadOnlyList<TaskCodeTraceabilityItem> FrontendComponents)
{
    public static TaskCodeTraceability Empty { get; } = new([], [], []);

    /// <summary>
    /// 將 task 的程式碼追蹤資訊轉成對外輸出的 traceability model。
    /// </summary>
    public TaskCodeTraceabilityModel ToModel()
    {
        return new(
            Api.Select(item => item.ToModel()).ToArray(),
            FrontendPages.Select(item => item.ToModel()).ToArray(),
            FrontendComponents.Select(item => item.ToModel()).ToArray());
    }
}