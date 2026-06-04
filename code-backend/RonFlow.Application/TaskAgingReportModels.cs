using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record TaskAgingThresholdOverrides(
    int? TodoThresholdDays,
    int? ActiveThresholdDays,
    int? ReviewThresholdDays);

public sealed record TaskAgingStateThresholdView(
    string StateKey,
    string StateLabel,
    int ThresholdDays);

public sealed record TaskAgingTaskItemView(
    Guid TaskId,
    string Title,
    WorkflowStateView CurrentState,
    DateTimeOffset EnteredStateAt,
    int AgingDays);

public sealed record TaskAgingReportView(
    Guid ProjectId,
    DateTimeOffset LastUpdatedAt,
    IReadOnlyList<TaskAgingStateThresholdView> Thresholds,
    IReadOnlyList<TaskAgingTaskItemView> Items);

public sealed class GetTaskAgingReportQueryService(
    ProjectAccessService projectAccessService,
    ICoreFlowReadStore readStore,
    TimeProvider timeProvider)
{
    public OwnedResourceQueryResult<TaskAgingReportView> Get(
        Guid currentUserId,
        Guid projectId,
        TaskAgingThresholdOverrides thresholdOverrides)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<TaskAgingReportView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<TaskAgingReportView>.Denied();
        }

        var board = readStore.GetProjectBoard(projectId);
        if (board is null)
        {
            return OwnedResourceQueryResult<TaskAgingReportView>.Missing();
        }

        var now = timeProvider.GetUtcNow();
        var thresholds = board.WorkflowStates
            .Where(state => state.IsCompletedState is false)
            .Select(state => new TaskAgingStateThresholdView(
                state.Key,
                state.Label,
                ResolveThresholdDays(state.Key, thresholdOverrides)))
            .ToArray();

        var thresholdLookup = thresholds.ToDictionary(item => item.StateKey, item => item.ThresholdDays, StringComparer.OrdinalIgnoreCase);

        var items = board.Tasks
            .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
            .Where(task => task.CurrentState.IsCompletedState is false)
            .Select(task => CreateTaskAgingItem(task, now))
            .Where(item => thresholdLookup.TryGetValue(item.CurrentState.Key, out var thresholdDays)
                && now - item.EnteredStateAt >= TimeSpan.FromDays(thresholdDays))
            .OrderByDescending(item => item.AgingDays)
            .ThenBy(item => item.EnteredStateAt)
            .ToArray();

        return OwnedResourceQueryResult<TaskAgingReportView>.Success(
            new TaskAgingReportView(projectId, now, thresholds, items));
    }

    private static TaskAgingTaskItemView CreateTaskAgingItem(TaskModel task, DateTimeOffset now)
    {
        var enteredStateAt = GetEnteredStateAt(task);
        var agingDays = Math.Max(0, (int)Math.Floor((now - enteredStateAt).TotalDays));

        return new TaskAgingTaskItemView(
            task.Id,
            task.Title,
            CoreFlowReadModelFactory.CreateWorkflowState(task.CurrentState),
            enteredStateAt,
            agingDays);
    }

    private static DateTimeOffset GetEnteredStateAt(TaskModel task)
    {
        return task.ActivityTimeline
            .Where(item => string.Equals(item.Type, "TaskStateChanged", StringComparison.Ordinal))
            .Select(item => item.OccurredAt)
            .DefaultIfEmpty(task.CreatedAt)
            .Last();
    }

    private static int ResolveThresholdDays(string stateKey, TaskAgingThresholdOverrides thresholdOverrides)
    {
        return stateKey.Trim().ToLowerInvariant() switch
        {
            "todo" => thresholdOverrides.TodoThresholdDays ?? 7,
            "active" => thresholdOverrides.ActiveThresholdDays ?? 3,
            "review" => thresholdOverrides.ReviewThresholdDays ?? 2,
            _ => 3,
        };
    }
}