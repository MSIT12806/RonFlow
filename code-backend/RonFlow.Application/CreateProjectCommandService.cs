using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class CreateProjectCommandService(IProjectRepository projectRepository, TimeProvider timeProvider)
{
    public CreateProjectResult Create(string? rawName)
    {
        return Create(Guid.Empty, rawName);
    }

    public CreateProjectResult Create(Guid currentUserId, string? rawName)
    {
        return Create(currentUserId, string.Empty, string.Empty, rawName);
    }

    public CreateProjectResult Create(Guid currentUserId, string currentUserName, string currentUserEmail, string? rawName)
    {
        if (!ProjectName.TryCreate(rawName, out var projectName))
        {
            return CreateProjectResult.Invalid("name", "專案名稱為必填欄位");
        }

        var project = Project.Create(currentUserId, currentUserName, currentUserEmail, projectName!, timeProvider.GetUtcNow(), DefaultWorkflow.CreateStates());
        projectRepository.Add(project);

        return CreateProjectResult.Success(CoreFlowCommandOutputFactory.CreateProject(project.ToModel()));
    }
}
