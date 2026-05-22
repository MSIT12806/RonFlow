using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetTrashedTasksQueryService(ProjectAccessService projectAccessService, ITaskRepository taskRepository)
{
    public OwnedResourceQueryResult<LifecycleTaskListView> Get(Guid currentUserId, Guid projectId)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<LifecycleTaskListView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<LifecycleTaskListView>.Denied();
        }

        var tasks = taskRepository.GetByProjectId(projectId)
            .Select(task => task.ToModel())
            .ToArray();

        return OwnedResourceQueryResult<LifecycleTaskListView>.Success(
            CoreFlowReadModelFactory.CreateLifecycleTaskList(access.Project!, tasks, TaskLifecycleState.Trashed));
    }
}