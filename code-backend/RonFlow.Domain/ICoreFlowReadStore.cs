namespace RonFlow.Domain;

public interface ICoreFlowReadStore
{
    IReadOnlyList<ProjectSummaryModel> GetProjects();

    ProjectBoardModel? GetProjectBoard(Guid projectId);

    TaskModel? GetTaskDetail(Guid projectId, Guid taskId);
}