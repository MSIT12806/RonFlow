namespace RonFlow.Domain;

public interface IProjectRepository
{
    IReadOnlyList<ProjectSummaryModel> GetProjects();

    void Add(Project project);

    ProjectBoardModel? GetBoard(Guid projectId);

    TaskModel? CreateTask(Guid projectId, TaskTitle title, DateTimeOffset createdAt);

    TaskModel? GetTask(Guid projectId, Guid taskId);
}