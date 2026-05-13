using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ChangeTaskStateCommandService(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public ChangeTaskStateResult Change(Guid projectId, Guid taskId, string stateKey)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return ChangeTaskStateResult.NotFound();
        }

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

        var changedAt = timeProvider.GetUtcNow();
        task.ChangeState(targetState, changedAt);
        taskRepository.Update(task);

        project.Touch(changedAt);
        projectRepository.Update(project);

        return ChangeTaskStateResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
