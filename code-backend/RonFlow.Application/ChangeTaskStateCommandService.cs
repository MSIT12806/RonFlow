using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ChangeTaskStateCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TaskMutationGuard taskMutationGuard,
    IWorkflowThroughputProjectionOutbox workflowThroughputProjectionOutbox,
    TimeProvider timeProvider)
{
    public ChangeTaskStateCommandService(
        IProjectRepository projectRepository,
        ITaskRepository taskRepository,
        TimeProvider timeProvider)
        : this(projectRepository, new ProjectAccessService(projectRepository), taskRepository, new TaskMutationGuard(new TaskContentEditLockService()), new NoOpWorkflowThroughputProjectionOutbox(), timeProvider)
    {
    }

    public ChangeTaskStateResult Change(Guid projectId, Guid taskId, string stateKey)
    {
        return Change(Guid.Empty, projectId, taskId, stateKey);
    }

    public ChangeTaskStateResult Change(Guid currentUserId, Guid projectId, Guid taskId, string stateKey)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return ChangeTaskStateResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return ChangeTaskStateResult.Denied();
        }

        var project = access.Project!;

        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return ChangeTaskStateResult.NotFound();
        }

        var targetState = project.FindWorkflowState(stateKey);
        if (targetState is null)
        {
            return ChangeTaskStateResult.Invalid("stateKey", "指定的狀態不存在於此專案 workflow");
        }

        if (!task.IsInFlow)
        {
            var activeChildTaskCount = taskRepository.GetByProjectId(projectId)
                .Count(projectTask => projectTask.ParentTaskId == task.Id && projectTask.LifecycleState == TaskLifecycleState.ActiveRecord);

            if (activeChildTaskCount > 0)
            {
                return ChangeTaskStateResult.Invalid("readyList", "父任務不可送進 Flow");
            }

            if (task.Subtasks.Count == 0)
            {
                return ChangeTaskStateResult.Invalid("readyList", "至少需要一筆完成條件才能送進 Flow");
            }

            if (task.EstimatedEffort is null)
            {
                return ChangeTaskStateResult.Invalid("estimatedEffort", "需要填寫預估耗時才能送進 Flow");
            }
        }

        var changedAt = timeProvider.GetUtcNow();
        var wasCompleted = task.CurrentState.IsCompletedState;
        var wasInFlow = task.IsInFlow;
        var mutationResult = task.ChangeState(
            taskMutationGuard.Authorize(currentUserId, taskId, TaskMutationKind.ChangeWorkflowState),
            targetState,
            changedAt);

        if (mutationResult.Locked)
        {
            return ChangeTaskStateResult.Locked();
        }

        taskRepository.Update(task);
        if (!wasInFlow)
        {
            workflowThroughputProjectionOutbox.EnqueueTaskCreated(project.Id, task.Id, changedAt);
        }

        workflowThroughputProjectionOutbox.EnqueueTaskStateChanged(project.Id, task.Id, targetState.Key, changedAt);
        if (!wasCompleted && targetState.IsCompletedState)
        {
            workflowThroughputProjectionOutbox.EnqueueTaskCompleted(project.Id, task.Id, changedAt);
        }

        if (wasCompleted && !targetState.IsCompletedState)
        {
            workflowThroughputProjectionOutbox.EnqueueTaskReopened(project.Id, task.Id, changedAt);
        }

        if (!wasCompleted && targetState.IsCompletedState)
        {
            CompleteParentsWhenAllChildrenAreDone(project, task, changedAt);
        }

        project.Touch(changedAt);
        projectRepository.Update(project);

        return ChangeTaskStateResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }

    private void CompleteParentsWhenAllChildrenAreDone(Project project, RonFlow.Domain.Task completedTask, DateTimeOffset changedAt)
    {
        var activeTasks = taskRepository.GetByProjectId(project.Id)
            .Where(projectTask => projectTask.LifecycleState == TaskLifecycleState.ActiveRecord)
            .ToList();
        var completedState = project.WorkflowStates.SingleOrDefault(state => state.IsCompletedState);
        var parentTaskId = completedTask.ParentTaskId;

        while (parentTaskId is not null && completedState is not null)
        {
            var parentTask = activeTasks.SingleOrDefault(projectTask => projectTask.Id == parentTaskId.Value);
            if (parentTask is null || parentTask.CurrentState.IsCompletedState)
            {
                return;
            }

            var childTasks = activeTasks
                .Where(projectTask => projectTask.ParentTaskId == parentTask.Id)
                .ToArray();

            if (childTasks.Length == 0 || childTasks.Any(childTask => childTask.CurrentState.IsCompletedState is false))
            {
                return;
            }

            if (parentTask.CompleteFromChildren(completedState, changedAt))
            {
                taskRepository.Update(parentTask);
                workflowThroughputProjectionOutbox.EnqueueTaskStateChanged(project.Id, parentTask.Id, completedState.Key, changedAt);
                workflowThroughputProjectionOutbox.EnqueueTaskCompleted(project.Id, parentTask.Id, changedAt);
            }

            parentTaskId = parentTask.ParentTaskId;
        }
    }
}
