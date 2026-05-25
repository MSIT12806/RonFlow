using System.Threading;

namespace RonFlow.Application;

public static class BoardReadObservabilityContext
{
    private static readonly AsyncLocal<BoardReadTimingSnapshot?> CurrentSnapshot = new();

    public static BoardReadTimingSnapshot Current => CurrentSnapshot.Value ??= new BoardReadTimingSnapshot();

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
    public double? ApplicationElapsedMs { get; set; }

    public double? StoreElapsedMs { get; set; }
}