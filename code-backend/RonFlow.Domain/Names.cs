namespace RonFlow.Domain;

public sealed record ProjectName
{
    private ProjectName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? rawValue, out ProjectName? projectName)
    {
        var normalizedValue = rawValue?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            projectName = null;
            return false;
        }

        projectName = new ProjectName(normalizedValue);
        return true;
    }
}

public sealed record TaskTitle
{
    private TaskTitle(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? rawValue, out TaskTitle? taskTitle)
    {
        var normalizedValue = rawValue?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            taskTitle = null;
            return false;
        }

        taskTitle = new TaskTitle(normalizedValue);
        return true;
    }
}