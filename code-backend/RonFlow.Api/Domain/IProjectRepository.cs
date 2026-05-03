namespace RonFlow.Domain;

public interface IProjectRepository
{
    IReadOnlyList<ProjectSummaryModel> GetProjects();

    void Add(Project project);

    ProjectBoardModel? GetBoard(Guid projectId);

    TaskModel? CreateTask(Guid projectId, TaskTitle title, DateTimeOffset createdAt);

    TaskModel? ChangeTaskState(Guid projectId, Guid taskId, string stateKey, DateTimeOffset changedAt);

    TaskModel? GetTask(Guid projectId, Guid taskId);
}