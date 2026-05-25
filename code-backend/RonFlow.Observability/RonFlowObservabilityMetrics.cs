using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RonFlow.Observability;

public static class RonFlowObservabilityMetrics
{
    public const string MeterName = "RonFlow.Observability";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> BoardRequestDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.request.duration",
        unit: "ms",
        description: "Elapsed time spent in the complete observed request.");
    private static readonly Histogram<double> BoardResponseStartDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.response_start.duration",
        unit: "ms",
        description: "Elapsed time from request start until response headers are about to be written.");
    private static readonly Histogram<double> BoardControllerDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.controller.duration",
        unit: "ms",
        description: "Elapsed time spent in the observed controller boundary.");
    private static readonly Histogram<double> BoardApplicationDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.application.duration",
        unit: "ms",
        description: "Elapsed time spent in the observed application service.");
    private static readonly Histogram<double> BoardStoreDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.store.duration",
        unit: "ms",
        description: "Elapsed time spent in the observed store.");
    private static readonly Histogram<double> BoardResultDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.result.duration",
        unit: "ms",
        description: "Elapsed time spent executing the observed result.");
    private static readonly Histogram<double> BoardCurrentUserDirectorySyncDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.current_user_directory_sync.duration",
        unit: "ms",
        description: "Elapsed time spent in CurrentUserDirectorySyncMiddleware during observed requests.");
    private static readonly Histogram<double> BoardActiveSessionDuration = Meter.CreateHistogram<double>(
        "ronflow.operation.active_session.duration",
        unit: "ms",
        description: "Elapsed time spent in RonFlowActiveSessionMiddleware during observed requests.");

    public static void RecordRequestDuration(string operationName, double durationMs)
    {
        BoardRequestDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardRequestDuration(double durationMs)
    {
        RecordRequestDuration(ObservedOperationNames.BoardRead, durationMs);
    }

    public static void RecordResponseStartDuration(string operationName, double durationMs)
    {
        BoardResponseStartDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardResponseStartDuration(double durationMs)
    {
        RecordResponseStartDuration(ObservedOperationNames.BoardRead, durationMs);
    }

    public static void RecordControllerDuration(string operationName, double durationMs)
    {
        BoardControllerDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardControllerDuration(double durationMs)
    {
        RecordControllerDuration(ObservedOperationNames.BoardRead, durationMs);
    }

    public static void RecordApplicationDuration(string operationName, double durationMs)
    {
        BoardApplicationDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardApplicationDuration(double durationMs)
    {
        RecordApplicationDuration(ObservedOperationNames.BoardRead, durationMs);
    }

    public static void RecordStoreDuration(string operationName, double durationMs)
    {
        BoardStoreDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardStoreDuration(double durationMs)
    {
        RecordStoreDuration(ObservedOperationNames.BoardRead, durationMs);
    }

    public static void RecordResultDuration(string operationName, double durationMs)
    {
        BoardResultDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardResultDuration(double durationMs)
    {
        RecordResultDuration(ObservedOperationNames.BoardRead, durationMs);
    }

    public static void RecordCurrentUserDirectorySyncDuration(string operationName, double durationMs)
    {
        BoardCurrentUserDirectorySyncDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardCurrentUserDirectorySyncDuration(double durationMs)
    {
        RecordCurrentUserDirectorySyncDuration(ObservedOperationNames.BoardRead, durationMs);
    }

    public static void RecordActiveSessionDuration(string operationName, double durationMs)
    {
        BoardActiveSessionDuration.Record(durationMs, new TagList { { "operation", operationName } });
    }

    public static void RecordBoardActiveSessionDuration(double durationMs)
    {
        RecordActiveSessionDuration(ObservedOperationNames.BoardRead, durationMs);
    }
}