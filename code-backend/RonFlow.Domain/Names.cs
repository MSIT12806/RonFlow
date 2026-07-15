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

public sealed record TaskEstimatedEffort
{
    private static readonly HashSet<string> SupportedUnits = new(StringComparer.Ordinal)
    {
        "minutes",
        "hours",
        "days",
    };

    private TaskEstimatedEffort(int value, string unit)
    {
        Value = value;
        Unit = unit;
    }

    public int Value { get; }

    public string Unit { get; }

    public static bool TryCreate(int? rawValue, string? rawUnit, out TaskEstimatedEffort? estimatedEffort)
    {
        var unit = rawUnit?.Trim();

        if (rawValue is null && string.IsNullOrWhiteSpace(unit))
        {
            estimatedEffort = null;
            return true;
        }

        if (rawValue is null || rawValue.Value <= 0 || string.IsNullOrWhiteSpace(unit) || !SupportedUnits.Contains(unit))
        {
            estimatedEffort = null;
            return false;
        }

        estimatedEffort = new TaskEstimatedEffort(rawValue.Value, unit);
        return true;
    }

    public TaskEstimatedEffortModel ToModel()
    {
        return new(Value, Unit);
    }

    public int ToMinutes()
    {
        return Unit switch
        {
            "minutes" => Value,
            "hours" => checked(Value * 60),
            // A workday is treated as eight working hours for reporting.
            "days" => checked(Value * 8 * 60),
            _ => 0,
        };
    }
}
