using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Application;

public sealed class DuplicateTaskSubtreeCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public DuplicateTaskSubtreeResult Duplicate(Guid currentUserId, Guid projectId, Guid sourceTaskId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return DuplicateTaskSubtreeResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return DuplicateTaskSubtreeResult.Denied();
        }

        var activeTasks = taskRepository.GetByProjectId(projectId)
            .Where(task => task.LifecycleState == TaskLifecycleState.ActiveRecord)
            .ToArray();
        var sourceTask = activeTasks.SingleOrDefault(task => task.Id == sourceTaskId);
        if (sourceTask is null)
        {
            return DuplicateTaskSubtreeResult.NotFound();
        }

        var copiedTitle = $"{sourceTask.Title}（複本）";
        if (!TaskTitle.TryCreate(copiedTitle, out var rootTitle))
        {
            return DuplicateTaskSubtreeResult.NotFound();
        }

        var changedAt = timeProvider.GetUtcNow();
        var project = access.Project!;
        var childrenByParentId = activeTasks
            .Where(task => task.ParentTaskId is not null)
            .GroupBy(task => task.ParentTaskId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(task => task.SortOrder)
                    .ThenBy(task => task.CreatedAt)
                    .ToArray());

        var rootTasks = activeTasks
            .Where(task => task.ParentTaskId is null)
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToArray();
        foreach (var (rootTask, index) in rootTasks.Select((task, index) => (task, index)))
        {
            rootTask.UpdateSortOrder(index + 1, changedAt, recordActivity: false);
            taskRepository.Update(rootTask);
        }

        var copiedRoot = DuplicateSubtree(
            sourceTask,
            parentCopyId: null,
            sortOrder: 0,
            rootTitle!,
            project.GetDefaultWorkflowState(),
            changedAt,
            childrenByParentId);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return DuplicateTaskSubtreeResult.Success(CoreFlowCommandOutputFactory.CreateTask(copiedRoot.ToModel()));
    }

    private DomainTask DuplicateSubtree(
        DomainTask sourceTask,
        Guid? parentCopyId,
        int sortOrder,
        TaskTitle copiedTitle,
        WorkflowState initialState,
        DateTimeOffset changedAt,
        IReadOnlyDictionary<Guid, DomainTask[]> childrenByParentId)
    {
        var copiedTask = DomainTask.Duplicate(
            sourceTask,
            copiedTitle,
            initialState,
            changedAt,
            sortOrder,
            parentCopyId);
        taskRepository.Add(copiedTask);

        if (!childrenByParentId.TryGetValue(sourceTask.Id, out var children))
        {
            return copiedTask;
        }

        foreach (var (child, index) in children.Select((child, index) => (child, index)))
        {
            var childTitle = $"{child.Title}（複本）";
            if (!TaskTitle.TryCreate(childTitle, out var copiedChildTitle))
            {
                continue;
            }

            DuplicateSubtree(
                child,
                copiedTask.Id,
                index,
                copiedChildTitle!,
                initialState,
                changedAt,
                childrenByParentId);
        }

        return copiedTask;
    }
}
