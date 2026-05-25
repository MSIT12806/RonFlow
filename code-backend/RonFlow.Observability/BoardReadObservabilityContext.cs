using System.Threading;

namespace RonFlow.Observability;

public static class BoardReadObservabilityContext
{
    private static readonly AsyncLocal<BoardReadTimingSnapshot?> CurrentSnapshot = new();

    public static BoardReadTimingSnapshot Current => CurrentSnapshot.Value ??= new BoardReadTimingSnapshot();

    public static bool TryGetCurrent(out BoardReadTimingSnapshot? snapshot)
    {
        snapshot = CurrentSnapshot.Value;
        return snapshot is not null;
    }

    public static void Reset()
    {
        CurrentSnapshot.Value = new BoardReadTimingSnapshot();
    }

    public static void Clear()
    {
        CurrentSnapshot.Value = null;
    }
}

public sealed class BoardReadTimingSnapshot
{
    public double? CurrentUserDirectorySyncElapsedMs { get; set; }

    public double? ActiveSessionElapsedMs { get; set; }

    public double? ControllerElapsedMs { get; set; }

    public double? ApplicationElapsedMs { get; set; }

    public double? StoreElapsedMs { get; set; }

    public double? ResultElapsedMs { get; set; }

    public double? ResponseStartElapsedMs { get; set; }

    public double? RequestElapsedMs { get; set; }
}