using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class CreateProjectCommandService(IProjectRepository projectRepository, TimeProvider timeProvider)
{
    public CreateProjectResult Create(string? rawName)
    {
        if (!ProjectName.TryCreate(rawName, out var projectName))
        {
            return CreateProjectResult.Invalid("name", "專案名稱為必填欄位");
        }

        var project = Project.Create(projectName!, timeProvider.GetUtcNow(), DefaultWorkflow.CreateStates());
        projectRepository.Add(project);

        return CreateProjectResult.Success(CoreFlowCommandOutputFactory.CreateProject(project.ToModel()));
    }
}
