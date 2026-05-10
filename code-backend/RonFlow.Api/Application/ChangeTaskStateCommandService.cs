using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ChangeTaskStateCommandService(IProjectRepository projectRepository, TimeProvider timeProvider)
{
    public ChangeTaskStateResult Change(Guid projectId, Guid taskId, string stateKey)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return ChangeTaskStateResult.NotFound();
        }

        var task = project.ChangeTaskState(taskId, stateKey, timeProvider.GetUtcNow());
        if (task is null)
        {
            return ChangeTaskStateResult.NotFound();
        }

        projectRepository.Update(project);

        return ChangeTaskStateResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}
