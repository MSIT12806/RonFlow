using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetTaskDetailQueryService(ICoreFlowReadStore readStore)
{
    public TaskDetailView? Get(Guid projectId, Guid taskId)
    {
        var task = readStore.GetTaskDetail(projectId, taskId);
        return task is null ? null : CoreFlowReadModelFactory.CreateTaskDetail(task);
    }
}
