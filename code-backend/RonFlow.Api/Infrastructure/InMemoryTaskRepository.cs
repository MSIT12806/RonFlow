using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Infrastructure;

public sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, DomainTask> tasks = [];

    public DomainTask? Get(Guid taskId)
    {
        lock (syncRoot)
        {
            return tasks.GetValueOrDefault(taskId);
        }
    }

    public IReadOnlyList<DomainTask> GetByProjectId(Guid projectId)
    {
        lock (syncRoot)
        {
            return tasks.Values
                .Where(task => task.ProjectId == projectId)
                .OrderBy(task => task.SortOrder)
                .ThenBy(task => task.CreatedAt)
                .ToArray();
        }
    }

    public void Add(DomainTask task)
    {
        lock (syncRoot)
        {
            tasks.Add(task.Id, task);
        }
    }

    public void Update(DomainTask task)
    {
        lock (syncRoot)
        {
            if (tasks.ContainsKey(task.Id))
            {
                tasks[task.Id] = task;
            }
        }
    }
}