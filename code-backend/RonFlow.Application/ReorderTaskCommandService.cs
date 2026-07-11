using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Application;

public sealed class ReorderTaskCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public ReorderTaskResult Reorder(Guid currentUserId, Guid projectId, Guid taskId, Guid targetTaskId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return ReorderTaskResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return ReorderTaskResult.Denied();
        }

        var project = access.Project!;

        var task = taskRepository.Get(taskId);
        var targetTask = taskRepository.Get(targetTaskId);

        if (task is null || targetTask is null || task.ProjectId != projectId || targetTask.ProjectId != projectId)
        {
            return ReorderTaskResult.NotFound();
        }

        if (task.IsInFlow is false || targetTask.IsInFlow is false || task.CurrentState.Key != targetTask.CurrentState.Key || task.Id == targetTask.Id)
        {
            return ReorderTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
        }

        var changedAt = timeProvider.GetUtcNow();
        var tasksInState = taskRepository.GetByProjectId(projectId)
            .Where(projectTask => projectTask.CurrentState.Key == task.CurrentState.Key)
            .Where(projectTask => projectTask.IsInFlow)
            .OrderBy(projectTask => projectTask.SortOrder)
            .ToList();

        tasksInState.RemoveAll(projectTask => projectTask.Id == task.Id);
        var targetIndex = tasksInState.FindIndex(projectTask => projectTask.Id == targetTaskId);

        if (targetIndex < 0)
        {
            return ReorderTaskResult.NotFound();
        }

        tasksInState.Insert(targetIndex, task);

        for (var index = 0; index < tasksInState.Count; index += 1)
        {
            var projectTask = tasksInState[index];
            var shouldRecordActivity = projectTask.Id == task.Id;
            projectTask.UpdateSortOrder(index, changedAt, shouldRecordActivity);
            taskRepository.Update(projectTask);
        }

        project.Touch(changedAt);
        projectRepository.Update(project);

        return ReorderTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }

    public ReorderTaskResult MoveInTree(
        Guid currentUserId,
        Guid projectId,
        Guid taskId,
        Guid? targetParentTaskId,
        Guid? targetSiblingTaskId,
        bool insertAfter)
    {
        if (targetParentTaskId is null && targetSiblingTaskId is null)
        {
            return ReorderTaskResult.Invalid("targetSiblingTaskId", "目標位置為必填欄位");
        }

        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return ReorderTaskResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return ReorderTaskResult.Denied();
        }

        var project = access.Project!;
        var activeTasks = taskRepository.GetByProjectId(projectId)
            .Where(projectTask => projectTask.LifecycleState == TaskLifecycleState.ActiveRecord)
            .ToList();
        var tasksById = activeTasks.ToDictionary(projectTask => projectTask.Id);

        if (!tasksById.TryGetValue(taskId, out var task))
        {
            return ReorderTaskResult.NotFound();
        }

        DomainTask? targetParent = null;
        if (targetParentTaskId is not null && !tasksById.TryGetValue(targetParentTaskId.Value, out targetParent))
        {
            return ReorderTaskResult.NotFound();
        }

        DomainTask? targetSibling = null;
        if (targetSiblingTaskId is not null && !tasksById.TryGetValue(targetSiblingTaskId.Value, out targetSibling))
        {
            return ReorderTaskResult.NotFound();
        }

        if (targetSibling?.Id == task.Id)
        {
            return ReorderTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
        }

        if (targetParentTaskId == task.Id)
        {
            return ReorderTaskResult.Invalid("targetParentTaskId", "任務不可放入自己底下");
        }

        if (WouldCreateTaskTreeCycle(targetParentTaskId, task.Id, tasksById))
        {
            return ReorderTaskResult.Invalid("targetParentTaskId", "任務不可放入自己的子任務底下");
        }

        if (targetSibling is not null && targetSibling.ParentTaskId != targetParentTaskId)
        {
            return ReorderTaskResult.Invalid("targetSiblingTaskId", "目標任務不在指定的父任務底下");
        }

        var sourceParentTaskId = task.ParentTaskId;
        var sourceSiblings = GetSiblingTasks(activeTasks, sourceParentTaskId);
        var originalSourceOrder = sourceSiblings.Select(projectTask => projectTask.Id).ToArray();
        sourceSiblings.RemoveAll(projectTask => projectTask.Id == task.Id);

        var destinationSiblings = sourceParentTaskId == targetParentTaskId
            ? sourceSiblings
            : GetSiblingTasks(activeTasks, targetParentTaskId).Where(projectTask => projectTask.Id != task.Id).ToList();

        var insertIndex = destinationSiblings.Count;
        if (targetSibling is not null)
        {
            insertIndex = destinationSiblings.FindIndex(projectTask => projectTask.Id == targetSibling.Id);
            if (insertIndex < 0)
            {
                return ReorderTaskResult.NotFound();
            }

            if (insertAfter)
            {
                insertIndex += 1;
            }
        }

        destinationSiblings.Insert(insertIndex, task);

        var parentChanged = sourceParentTaskId != targetParentTaskId;
        var orderingChanged = parentChanged
            || !originalSourceOrder.SequenceEqual(destinationSiblings.Select(projectTask => projectTask.Id));

        if (!orderingChanged)
        {
            return ReorderTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
        }

        var changedAt = timeProvider.GetUtcNow();
        task.UpdateTreePosition(targetParentTaskId, changedAt, recordActivity: true);
        taskRepository.Update(task);

        ReindexSiblingTasks(sourceSiblings, changedAt);
        if (!ReferenceEquals(sourceSiblings, destinationSiblings))
        {
            ReindexSiblingTasks(destinationSiblings, changedAt);
        }
        else
        {
            ReindexSiblingTasks(destinationSiblings, changedAt);
        }

        if (parentChanged && targetParent is not null)
        {
            targetParent.MarkChildTaskAdded(TaskMutationAuthorization.Granted(TaskMutationKind.CreateChildTask), changedAt);
            taskRepository.Update(targetParent);
        }

        project.Touch(changedAt);
        projectRepository.Update(project);

        return ReorderTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }

    private static List<DomainTask> GetSiblingTasks(IEnumerable<DomainTask> tasks, Guid? parentTaskId)
    {
        return tasks
            .Where(projectTask => projectTask.ParentTaskId == parentTaskId)
            .OrderBy(projectTask => projectTask.SortOrder)
            .ThenBy(projectTask => projectTask.CreatedAt)
            .ToList();
    }

    private void ReindexSiblingTasks(IEnumerable<DomainTask> siblingTasks, DateTimeOffset changedAt)
    {
        foreach (var (projectTask, index) in siblingTasks.Select((projectTask, index) => (projectTask, index)))
        {
            projectTask.UpdateSortOrder(index, changedAt, recordActivity: false);
            taskRepository.Update(projectTask);
        }
    }

    private static bool WouldCreateTaskTreeCycle(
        Guid? targetParentTaskId,
        Guid taskId,
        IReadOnlyDictionary<Guid, DomainTask> tasksById)
    {
        var visitedTaskIds = new HashSet<Guid> { taskId };
        var currentParentTaskId = targetParentTaskId;

        while (currentParentTaskId is not null)
        {
            if (!visitedTaskIds.Add(currentParentTaskId.Value))
            {
                return true;
            }

            if (!tasksById.TryGetValue(currentParentTaskId.Value, out var parentTask))
            {
                return false;
            }

            currentParentTaskId = parentTask.ParentTaskId;
        }

        return false;
    }
}
