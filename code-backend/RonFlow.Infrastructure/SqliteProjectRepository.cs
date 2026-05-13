using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class SqliteProjectRepository(SqliteCoreFlowStore store) : IProjectRepository
{
    public IReadOnlyList<ProjectSummaryModel> GetProjects()
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM Projects";

        using var reader = command.ExecuteReader();
        var projects = new List<ProjectSummaryModel>();

        while (reader.Read())
        {
            var project = CoreFlowJsonSerializer.DeserializeProject(reader.GetString(0));
            projects.Add(project.ToSummaryModel());
        }

        return projects
            .OrderByDescending(project => project.UpdatedAt)
            .ToArray();
    }

    public Project? Get(Guid projectId)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM Projects WHERE Id = $id";
        command.Parameters.AddWithValue("$id", projectId.ToString());

        var json = command.ExecuteScalar() as string;
        return json is null ? null : CoreFlowJsonSerializer.DeserializeProject(json);
    }

    public void Add(Project project)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Projects (Id, Data) VALUES ($id, $data)";
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$data", CoreFlowJsonSerializer.Serialize(project));
        command.ExecuteNonQuery();
    }

    public void Update(Project project)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Projects SET Data = $data WHERE Id = $id";
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$data", CoreFlowJsonSerializer.Serialize(project));
        command.ExecuteNonQuery();
    }
}