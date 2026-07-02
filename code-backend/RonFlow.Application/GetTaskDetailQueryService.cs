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
        return GetTaskDetailView(projectId, taskId);
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

        var task = GetTaskDetailView(projectId, taskId);
        return task is null
            ? OwnedResourceQueryResult<TaskDetailView>.Missing()
            : OwnedResourceQueryResult<TaskDetailView>.Success(task);
    }

    private TaskDetailView? GetTaskDetailView(Guid projectId, Guid taskId)
    {
        var board = readStore.GetProjectBoard(projectId);
        var task = board?.Tasks.SingleOrDefault(item => item.Id == taskId);
        if (board is null || task is null)
        {
            return null;
        }

        var childTasks = board.Tasks
            .Where(item => item.LifecycleState == TaskLifecycleState.ActiveRecord)
            .ToArray();

        return CoreFlowReadModelFactory.CreateTaskDetail(task, childTasks);
    }
}
