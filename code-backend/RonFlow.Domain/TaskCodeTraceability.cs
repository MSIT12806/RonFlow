namespace RonFlow.Domain;

public sealed record TaskCodeTraceabilityItem(string ChangeType, string Target)
{
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

    public TaskCodeTraceabilityModel ToModel()
    {
        return new(
            Api.Select(item => item.ToModel()).ToArray(),
            FrontendPages.Select(item => item.ToModel()).ToArray(),
            FrontendComponents.Select(item => item.ToModel()).ToArray());
    }
}