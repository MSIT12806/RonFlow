using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ReorderTaskCommandService(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public ReorderTaskResult Reorder(Guid projectId, Guid taskId, Guid targetTaskId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return ReorderTaskResult.NotFound();
        }

        var task = taskRepository.Get(taskId);
        var targetTask = taskRepository.Get(targetTaskId);

        if (task is null || targetTask is null || task.ProjectId != projectId || targetTask.ProjectId != projectId)
        {
            return ReorderTaskResult.NotFound();
        }

        if (task.CurrentState.Key != targetTask.CurrentState.Key || task.Id == targetTask.Id)
        {
            return ReorderTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
        }

        var changedAt = timeProvider.GetUtcNow();
        var tasksInState = taskRepository.GetByProjectId(projectId)
            .Where(projectTask => projectTask.CurrentState.Key == task.CurrentState.Key)
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
}