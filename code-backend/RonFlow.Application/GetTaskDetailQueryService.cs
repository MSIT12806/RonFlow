using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetTaskDetailQueryService(ProjectAccessService projectAccessService, ICoreFlowReadStore readStore)
{
    public GetTaskDetailQueryService(ICoreFlowReadStore readStore)
        : this(null!, readStore)
    {
    }

    public TaskDetailView? Get(Guid projectId, Guid taskId)
    {
        var task = readStore.GetTaskDetail(projectId, taskId);
        return task is null ? null : CoreFlowReadModelFactory.CreateTaskDetail(task);
    }

    public OwnedResourceQueryResult<TaskDetailView> Get(Guid currentUserId, Guid projectId, Guid taskId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<TaskDetailView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<TaskDetailView>.Denied();
        }

        var task = readStore.GetTaskDetail(projectId, taskId);
        return task is null
            ? OwnedResourceQueryResult<TaskDetailView>.Missing()
            : OwnedResourceQueryResult<TaskDetailView>.Success(CoreFlowReadModelFactory.CreateTaskDetail(task));
    }
}
