using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetTaskDetailQueryService(IProjectRepository projectRepository)
{
    public TaskDetailView? Get(Guid projectId, Guid taskId)
    {
        var project = projectRepository.Get(projectId);
        var task = project?.GetTask(taskId);
        return task is null ? null : CoreFlowReadModelFactory.CreateTaskDetail(task.ToModel());
    }
}
