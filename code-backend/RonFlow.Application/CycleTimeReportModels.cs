using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record CycleTimeMetricSummaryView(
    int SampleCount,
    double? AverageHours,
    double? MedianHours,
    double? P90Hours);

public sealed record CycleTimeStateTransitionSummaryView(
    string FromStateKey,
    string FromStateLabel,
    string ToStateKey,
    string ToStateLabel,
    CycleTimeMetricSummaryView Duration);

public sealed record CycleTimeReportView(
    Guid ProjectId,
    DateOnly CompletedFrom,
    DateOnly CompletedTo,
    DateTimeOffset LastUpdatedAt,
    CycleTimeMetricSummaryView LeadTime,
    CycleTimeMetricSummaryView CycleTime,
    IReadOnlyList<CycleTimeStateTransitionSummaryView> StateTransitions);

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

        var stateTransitions = CreateStateTransitionSummaries(board.WorkflowStates, completedTasks);

        return OwnedResourceQueryResult<CycleTimeReportView>.Success(new CycleTimeReportView(
            projectId,
            effectiveFrom,
            effectiveTo,
            now,
            CreateSummary(leadTimeSamples),
            CreateSummary(cycleTimeSamples),
            stateTransitions));
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

    private static IReadOnlyList<CycleTimeStateTransitionSummaryView> CreateStateTransitionSummaries(
        IReadOnlyList<WorkflowStateModel> workflowStates,
        IReadOnlyList<TaskModel> completedTasks)
    {
        var transitions = new[]
        {
            ("todo", "active"),
            ("active", "review"),
            ("review", "done"),
        };

        var statesByKey = workflowStates.ToDictionary(state => state.Key, StringComparer.OrdinalIgnoreCase);
        var summaries = new List<CycleTimeStateTransitionSummaryView>();
        foreach (var (fromStateKey, toStateKey) in transitions)
        {
            if (!statesByKey.TryGetValue(fromStateKey, out var fromState)
                || !statesByKey.TryGetValue(toStateKey, out var toState))
            {
                continue;
            }

            var samples = completedTasks
                .Select(task => TryGetTransitionDurationHours(task, workflowStates, fromState.Key, toState.Key, out var durationHours)
                    ? (double?)durationHours
                    : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();

            summaries.Add(new CycleTimeStateTransitionSummaryView(
                fromState.Key,
                fromState.Label,
                toState.Key,
                toState.Label,
                CreateSummary(samples)));
        }

        return summaries;
    }

    private static bool TryGetTransitionDurationHours(
        TaskModel task,
        IReadOnlyList<WorkflowStateModel> workflowStates,
        string fromStateKey,
        string toStateKey,
        out double durationHours)
    {
        durationHours = default;
        if (task.CompletedAt is null)
        {
            return false;
        }

        var orderedEntries = CreateStateEntries(task, workflowStates, task.CompletedAt.Value);
        var toStateIndex = -1;
        for (var index = orderedEntries.Count - 1; index >= 0; index -= 1)
        {
            if (string.Equals(orderedEntries[index].StateKey, toStateKey, StringComparison.OrdinalIgnoreCase))
            {
                toStateIndex = index;
                break;
            }
        }

        if (toStateIndex <= 0)
        {
            return false;
        }

        for (var index = toStateIndex - 1; index >= 0; index -= 1)
        {
            if (!string.Equals(orderedEntries[index].StateKey, fromStateKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            durationHours = (orderedEntries[toStateIndex].EnteredAt - orderedEntries[index].EnteredAt).TotalHours;
            return durationHours >= 0;
        }

        return false;
    }

    private static IReadOnlyList<StateEntry> CreateStateEntries(
        TaskModel task,
        IReadOnlyList<WorkflowStateModel> workflowStates,
        DateTimeOffset completedAt)
    {
        var statesByLabel = workflowStates
            .GroupBy(state => state.Label, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var initialState = workflowStates.FirstOrDefault(state => state.IsInitialState) ?? workflowStates.First();
        var entries = new List<StateEntry> { new(initialState.Key, task.CreatedAt) };

        foreach (var item in task.ActivityTimeline
            .Where(item => string.Equals(item.Type, "TaskStateChanged", StringComparison.Ordinal))
            .Where(item => item.OccurredAt <= completedAt)
            .OrderBy(item => item.OccurredAt))
        {
            const string prefix = "任務狀態已變更為 ";
            if (!item.Message.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var stateLabel = item.Message[prefix.Length..];
            if (!statesByLabel.TryGetValue(stateLabel, out var state))
            {
                continue;
            }

            entries.Add(new StateEntry(state.Key, item.OccurredAt));
        }

        return entries;
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

    private sealed record StateEntry(string StateKey, DateTimeOffset EnteredAt);
}
