using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class GetProjectsQueryService(IProjectRepository projectRepository)
{
    public ProjectListView Get()
    {
        return CoreFlowReadModelFactory.CreateProjectList(projectRepository.GetProjects());
    }
}

public sealed class GetProjectBoardQueryService(IProjectRepository projectRepository)
{
    public ProjectBoardView? Get(Guid projectId)
    {
        var board = projectRepository.GetBoard(projectId);
        return board is null ? null : CoreFlowReadModelFactory.CreateProjectBoard(board);
    }
}

public sealed class GetTaskDetailQueryService(IProjectRepository projectRepository)
{
    public TaskDetailView? Get(Guid projectId, Guid taskId)
    {
        var project = projectRepository.Get(projectId);
        var task = project?.GetTask(taskId);
        return task is null ? null : CoreFlowReadModelFactory.CreateTaskDetail(task.ToModel());
    }
}