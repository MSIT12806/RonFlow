using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record CompletedTasksByMonthTaskView(
    Guid TaskId,
    string Title,
    DateTimeOffset CompletedAt);

public sealed record CompletedTasksByMonthBucketView(
    DateOnly MonthStart,
    IReadOnlyList<CompletedTasksByMonthTaskView> Tasks);

public sealed record CompletedTasksByMonthReportView(
    Guid ProjectId,
    DateOnly AnchorMonth,
    int MonthCount,
    DateTimeOffset LastUpdatedAt,
    bool CanMoveNewer,
    bool CanMoveOlder,
    IReadOnlyList<CompletedTasksByMonthBucketView> Months);

public sealed class GetCompletedTasksByMonthReportQueryService(
    ProjectAccessService projectAccessService,
    ICoreFlowReadStore readStore,
    TimeProvider timeProvider)
{
    public OwnedResourceQueryResult<CompletedTasksByMonthReportView> Get(
        Guid currentUserId,
        Guid projectId,
        DateOnly? anchorMonth,
        int? monthCount)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<CompletedTasksByMonthReportView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<CompletedTasksByMonthReportView>.Denied();
        }

        var board = readStore.GetProjectBoard(projectId);
        if (board is null)
        {
            return OwnedResourceQueryResult<CompletedTasksByMonthReportView>.Missing();
        }

        var now = timeProvider.GetUtcNow();
        var currentMonth = new DateOnly(now.Year, now.Month, 1);
        var effectiveAnchorMonth = NormalizeMonth(anchorMonth ?? currentMonth);
        if (effectiveAnchorMonth > currentMonth)
        {
            effectiveAnchorMonth = currentMonth;
        }

        var effectiveMonthCount = monthCount ?? 3;
        var completedTasks = board.Tasks
            .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
            .Where(task => task.CompletedAt is not null)
            .ToArray();

        var months = Enumerable.Range(0, effectiveMonthCount)
            .Select(offset => effectiveAnchorMonth.AddMonths(-offset))
            .Select(monthStart => CreateBucket(monthStart, completedTasks))
            .ToArray();

        var lastVisibleMonth = effectiveAnchorMonth.AddMonths(-(effectiveMonthCount - 1));
        var canMoveOlder = completedTasks.Any(task =>
            task.CompletedAt is not null
            && NormalizeMonth(DateOnly.FromDateTime(task.CompletedAt.Value.UtcDateTime)) < lastVisibleMonth);

        return OwnedResourceQueryResult<CompletedTasksByMonthReportView>.Success(new CompletedTasksByMonthReportView(
            projectId,
            effectiveAnchorMonth,
            effectiveMonthCount,
            now,
            effectiveAnchorMonth < currentMonth,
            canMoveOlder,
            months));
    }

    private static CompletedTasksByMonthBucketView CreateBucket(DateOnly monthStart, IReadOnlyList<TaskModel> completedTasks)
    {
        var nextMonth = monthStart.AddMonths(1);
        var items = completedTasks
            .Where(task => task.CompletedAt is not null)
            .Where(task =>
            {
                var completedDate = DateOnly.FromDateTime(task.CompletedAt!.Value.UtcDateTime);
                return completedDate >= monthStart && completedDate < nextMonth;
            })
            .OrderByDescending(task => task.CompletedAt)
            .ThenBy(task => task.Title, StringComparer.Ordinal)
            .Select(task => new CompletedTasksByMonthTaskView(task.Id, task.Title, task.CompletedAt!.Value))
            .ToArray();

        return new CompletedTasksByMonthBucketView(monthStart, items);
    }

    private static DateOnly NormalizeMonth(DateOnly value)
    {
        return new DateOnly(value.Year, value.Month, 1);
    }
}
