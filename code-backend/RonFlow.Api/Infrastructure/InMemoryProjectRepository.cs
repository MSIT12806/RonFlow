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

    public ProjectBoardModel? GetBoard(Guid projectId)
    {
        lock (syncRoot)
        {
            return projects.TryGetValue(projectId, out var project)
                ? project.ToBoardModel()
                : null;
        }
    }

    public TaskModel? CreateTask(Guid projectId, TaskTitle title, DateTimeOffset createdAt)
    {
        lock (syncRoot)
        {
            if (!projects.TryGetValue(projectId, out var project))
            {
                return null;
            }

            return project.CreateTask(title, createdAt).ToModel();
        }
    }

    public TaskModel? GetTask(Guid projectId, Guid taskId)
    {
        lock (syncRoot)
        {
            if (!projects.TryGetValue(projectId, out var project))
            {
                return null;
            }

            return project.GetTask(taskId)?.ToModel();
        }
    }

    public TaskModel? ChangeTaskState(Guid projectId, Guid taskId, string stateKey, DateTimeOffset changedAt)
    {
        lock (syncRoot)
        {
            if (!projects.TryGetValue(projectId, out var project))
            {
                return null;
            }

            return project.ChangeTaskState(taskId, stateKey, changedAt)?.ToModel();
        }
    }
}