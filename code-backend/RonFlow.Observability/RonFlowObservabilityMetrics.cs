using System.Diagnostics.Metrics;

namespace RonFlow.Observability;

public static class RonFlowObservabilityMetrics
{
    public const string MeterName = "RonFlow.Observability";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> BoardControllerDuration = Meter.CreateHistogram<double>(
        "ronflow.board.controller.duration",
        unit: "ms",
        description: "Elapsed time spent in the board read controller boundary.");
    private static readonly Histogram<double> BoardApplicationDuration = Meter.CreateHistogram<double>(
        "ronflow.board.application.duration",
        unit: "ms",
        description: "Elapsed time spent in the board read application service.");
    private static readonly Histogram<double> BoardStoreDuration = Meter.CreateHistogram<double>(
        "ronflow.board.store.duration",
        unit: "ms",
        description: "Elapsed time spent in the board read store.");

    public static void RecordBoardControllerDuration(double durationMs)
    {
        BoardControllerDuration.Record(durationMs);
    }

    public static void RecordBoardApplicationDuration(double durationMs)
    {
        BoardApplicationDuration.Record(durationMs);
    }

    public static void RecordBoardStoreDuration(double durationMs)
    {
        BoardStoreDuration.Record(durationMs);
    }
}