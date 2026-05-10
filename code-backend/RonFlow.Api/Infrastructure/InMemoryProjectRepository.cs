using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, Project> projects = [];

    public IReadOnlyList<ProjectSummaryModel> GetProjects()
    {
        lock (syncRoot)
        {
            return projects.Values
                .OrderByDescending(project => project.UpdatedAt)
                .Select(project => project.ToSummaryModel())
                .ToArray();
        }
    }

    public void Add(Project project)
    {
        lock (syncRoot)
        {
            projects.Add(project.Id, project);
        }
    }

    public Project? Get(Guid projectId)
    {
        lock (syncRoot)
        {
            return projects.GetValueOrDefault(projectId);
        }
    }

    public void Update(Project project)
    {
        lock (syncRoot)
        {
            if (projects.ContainsKey(project.Id))
            {
                projects[project.Id] = project;
            }
        }
    }

    public ProjectBoardModel? GetBoard(Guid projectId)
    {
        lock (syncRoot)
        {
            return projects.TryGetValue(projectId, out var project)
                ? project.ToBoardModel()
                : null;
        }
    }
}