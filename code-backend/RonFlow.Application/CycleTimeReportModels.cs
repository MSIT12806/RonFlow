using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record CycleTimeMetricSummaryView(
    int SampleCount,
    double? AverageHours,
    double? MedianHours,
    double? P90Hours);

public sealed record CycleTimeReportView(
    Guid ProjectId,
    DateOnly CompletedFrom,
    DateOnly CompletedTo,
    DateTimeOffset LastUpdatedAt,
    CycleTimeMetricSummaryView LeadTime,
    CycleTimeMetricSummaryView CycleTime);

public sealed class GetCycleTimeReportQueryService(
    ProjectAccessService projectAccessService,
    ICoreFlowReadStore readStore,
    TimeProvider timeProvider)
{
    public OwnedResourceQueryResult<CycleTimeReportView> Get(
        Guid currentUserId,
        Guid projectId,
        DateOnly? completedFrom,
        DateOnly? completedTo)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<CycleTimeReportView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<CycleTimeReportView>.Denied();
        }

        var board = readStore.GetProjectBoard(projectId);
        if (board is null)
        {
            return OwnedResourceQueryResult<CycleTimeReportView>.Missing();
        }

        var now = timeProvider.GetUtcNow();
        var effectiveTo = completedTo ?? DateOnly.FromDateTime(now.UtcDateTime);
        var effectiveFrom = completedFrom ?? effectiveTo.AddDays(-29);

        var completedTasks = board.Tasks
            .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
            .Where(task => task.CompletedAt is not null)
            .Where(task => IsWithinDateRange(task.CompletedAt!.Value, effectiveFrom, effectiveTo))
            .ToArray();

        var leadTimeSamples = completedTasks
            .Select(task => (task.CompletedAt!.Value - task.CreatedAt).TotalHours)
            .ToArray();

        var activeStateLabel = board.WorkflowStates.FirstOrDefault(state => string.Equals(state.Key, "active", StringComparison.OrdinalIgnoreCase))?.Label;
        var cycleTimeSamples = completedTasks
            .Select(task => TryGetActiveEnteredAt(task, activeStateLabel, out var activeEnteredAt)
                ? (double?) (task.CompletedAt!.Value - activeEnteredAt).TotalHours
                : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return OwnedResourceQueryResult<CycleTimeReportView>.Success(new CycleTimeReportView(
            projectId,
            effectiveFrom,
            effectiveTo,
            now,
            CreateSummary(leadTimeSamples),
            CreateSummary(cycleTimeSamples)));
    }

    private static bool IsWithinDateRange(DateTimeOffset completedAt, DateOnly completedFrom, DateOnly completedTo)
    {
        var completedDate = DateOnly.FromDateTime(completedAt.UtcDateTime);
        return completedDate >= completedFrom && completedDate <= completedTo;
    }

    private static bool TryGetActiveEnteredAt(TaskModel task, string? activeStateLabel, out DateTimeOffset enteredAt)
    {
        enteredAt = default;
        if (string.IsNullOrWhiteSpace(activeStateLabel) || task.CompletedAt is null)
        {
            return false;
        }

        var activeMessage = $"任務狀態已變更為 {activeStateLabel}";
        var candidate = task.ActivityTimeline
            .Where(item => string.Equals(item.Type, "TaskStateChanged", StringComparison.Ordinal))
            .Where(item => string.Equals(item.Message, activeMessage, StringComparison.Ordinal))
            .Where(item => item.OccurredAt <= task.CompletedAt.Value)
            .Select(item => item.OccurredAt)
            .LastOrDefault();

        if (candidate == default)
        {
            return false;
        }

        enteredAt = candidate;
        return true;
    }

    private static CycleTimeMetricSummaryView CreateSummary(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return new CycleTimeMetricSummaryView(0, null, null, null);
        }

        var ordered = samples.OrderBy(value => value).ToArray();
        return new CycleTimeMetricSummaryView(
            ordered.Length,
            ordered.Average(),
            CalculateMedian(ordered),
            CalculateP90(ordered));
    }

    private static double CalculateMedian(IReadOnlyList<double> orderedSamples)
    {
        if (orderedSamples.Count % 2 == 1)
        {
            return orderedSamples[orderedSamples.Count / 2];
        }

        var upperIndex = orderedSamples.Count / 2;
        var lowerIndex = upperIndex - 1;
        return (orderedSamples[lowerIndex] + orderedSamples[upperIndex]) / 2d;
    }

    private static double CalculateP90(IReadOnlyList<double> orderedSamples)
    {
        var rank = (int)Math.Ceiling(orderedSamples.Count * 0.9d);
        return orderedSamples[Math.Max(0, rank - 1)];
    }
}