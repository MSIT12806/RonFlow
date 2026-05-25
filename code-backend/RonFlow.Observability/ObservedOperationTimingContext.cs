using System.Threading;

namespace RonFlow.Observability;

public static class ObservedOperationTimingContext
{
    private static readonly AsyncLocal<ObservedOperationTimingSnapshot?> CurrentSnapshot = new();

    public static bool TryGetCurrent(out ObservedOperationTimingSnapshot? snapshot)
    {
        snapshot = CurrentSnapshot.Value;
        return snapshot is not null;
    }

    public static void Reset(string operationName)
    {
        CurrentSnapshot.Value = new ObservedOperationTimingSnapshot
        {
            OperationName = operationName,
        };
    }

    public static void Clear()
    {
        CurrentSnapshot.Value = null;
    }
}

public sealed class ObservedOperationTimingSnapshot
{
    public required string OperationName { get; init; }

    public double? CurrentUserDirectorySyncElapsedMs { get; set; }

    public double? ActiveSessionElapsedMs { get; set; }

    public double? ControllerElapsedMs { get; set; }

    public double? ApplicationElapsedMs { get; set; }

    public double? StoreElapsedMs { get; set; }

    public double? ResultElapsedMs { get; set; }

    public double? ResponseStartElapsedMs { get; set; }

    public double? RequestElapsedMs { get; set; }
}