namespace RonFlow.Domain;

public interface IProjectRepository
{
    IReadOnlyList<ProjectSummaryModel> GetProjects();

    Project? Get(Guid projectId);

    void Add(Project project);

    void Update(Project project);
}