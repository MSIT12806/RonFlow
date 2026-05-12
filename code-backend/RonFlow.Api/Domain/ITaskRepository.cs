namespace RonFlow.Domain;

public interface ITaskRepository
{
    Task? Get(Guid taskId);

    IReadOnlyList<Task> GetByProjectId(Guid projectId);

    void Add(Task task);

    void Update(Task task);
}