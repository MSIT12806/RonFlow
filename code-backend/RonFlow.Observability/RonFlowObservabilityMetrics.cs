using System.Diagnostics.Metrics;

namespace RonFlow.Observability;

public static class RonFlowObservabilityMetrics
{
    public const string MeterName = "RonFlow.Observability";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> BoardRequestDuration = Meter.CreateHistogram<double>(
        "ronflow.board.request.duration",
        unit: "ms",
        description: "Elapsed time spent in the complete board read request.");
    private static readonly Histogram<double> BoardResponseStartDuration = Meter.CreateHistogram<double>(
        "ronflow.board.response_start.duration",
        unit: "ms",
        description: "Elapsed time from board read request start until response headers are about to be written.");
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
    private static readonly Histogram<double> BoardResultDuration = Meter.CreateHistogram<double>(
        "ronflow.board.result.duration",
        unit: "ms",
        description: "Elapsed time spent executing the board read result.");
    private static readonly Histogram<double> BoardCurrentUserDirectorySyncDuration = Meter.CreateHistogram<double>(
        "ronflow.board.current_user_directory_sync.duration",
        unit: "ms",
        description: "Elapsed time spent in CurrentUserDirectorySyncMiddleware during board reads.");
    private static readonly Histogram<double> BoardActiveSessionDuration = Meter.CreateHistogram<double>(
        "ronflow.board.active_session.duration",
        unit: "ms",
        description: "Elapsed time spent in RonFlowActiveSessionMiddleware during board reads.");

    public static void RecordBoardRequestDuration(double durationMs)
    {
        BoardRequestDuration.Record(durationMs);
    }

    public static void RecordBoardResponseStartDuration(double durationMs)
    {
        BoardResponseStartDuration.Record(durationMs);
    }

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

    public static void RecordBoardResultDuration(double durationMs)
    {
        BoardResultDuration.Record(durationMs);
    }

    public static void RecordBoardCurrentUserDirectorySyncDuration(double durationMs)
    {
        BoardCurrentUserDirectorySyncDuration.Record(durationMs);
    }

    public static void RecordBoardActiveSessionDuration(double durationMs)
    {
        BoardActiveSessionDuration.Record(durationMs);
    }
}