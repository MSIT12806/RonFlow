using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Infrastructure;

public sealed class SqliteTaskRepository(SqliteCoreFlowStore store) : ITaskRepository
{
    public DomainTask? Get(Guid taskId)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM Tasks WHERE Id = $id";
        command.Parameters.AddWithValue("$id", taskId.ToString());

        var json = command.ExecuteScalar() as string;
        return json is null ? null : CoreFlowJsonSerializer.DeserializeTask(json);
    }

    public IReadOnlyList<DomainTask> GetByProjectId(Guid projectId)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM Tasks";

        using var reader = command.ExecuteReader();
        var tasks = new List<DomainTask>();

        while (reader.Read())
        {
            var task = CoreFlowJsonSerializer.DeserializeTask(reader.GetString(0));
            if (task.ProjectId == projectId)
            {
                tasks.Add(task);
            }
        }

        return tasks
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToArray();
    }

    public void Add(DomainTask task)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Tasks (Id, Data) VALUES ($id, $data)";
        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$data", CoreFlowJsonSerializer.Serialize(task));
        command.ExecuteNonQuery();
    }

    public void Update(DomainTask task)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tasks SET Data = $data WHERE Id = $id";
        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$data", CoreFlowJsonSerializer.Serialize(task));
        command.ExecuteNonQuery();
    }
}