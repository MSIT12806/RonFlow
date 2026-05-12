using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class SqliteCoreFlowReadStore(SqliteCoreFlowStore store) : ICoreFlowReadStore
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

    public ProjectBoardModel? GetProjectBoard(Guid projectId)
    {
        using var connection = store.OpenConnection();
        using var projectCommand = connection.CreateCommand();
        projectCommand.CommandText = "SELECT Data FROM Projects WHERE Id = $id";
        projectCommand.Parameters.AddWithValue("$id", projectId.ToString());

        var projectJson = projectCommand.ExecuteScalar() as string;
        if (projectJson is null)
        {
            return null;
        }

        var project = CoreFlowJsonSerializer.DeserializeProject(projectJson);
        using var taskCommand = connection.CreateCommand();
        taskCommand.CommandText = "SELECT Data FROM Tasks";

        using var reader = taskCommand.ExecuteReader();
        var taskModels = new List<TaskModel>();

        while (reader.Read())
        {
            var task = CoreFlowJsonSerializer.DeserializeTask(reader.GetString(0));
            if (task.ProjectId == projectId)
            {
                taskModels.Add(task.ToModel());
            }
        }

        return new ProjectBoardModel(
            project.Id,
            project.Name,
            project.WorkflowStates.Select(state => state.ToModel()).ToArray(),
            taskModels.OrderBy(task => task.SortOrder).ThenBy(task => task.CreatedAt).ToArray());
    }

    public TaskModel? GetTaskDetail(Guid projectId, Guid taskId)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM Tasks WHERE Id = $id";
        command.Parameters.AddWithValue("$id", taskId.ToString());

        var json = command.ExecuteScalar() as string;
        if (json is null)
        {
            return null;
        }

        var task = CoreFlowJsonSerializer.DeserializeTask(json);
        return task.ProjectId == projectId ? task.ToModel() : null;
    }
}