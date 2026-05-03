using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record ValidationError(string Field, string Message);

public sealed record CreateProjectResult(CreateProjectOutput? Project, ValidationError? ValidationError)
{
    public static CreateProjectResult Success(CreateProjectOutput project)
    {
        return new(project, null);
    }

    public static CreateProjectResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message));
    }
}

public sealed record CreateTaskResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool ProjectNotFound)
{
    public static CreateTaskResult Success(CreateTaskOutput task)
    {
        return new(task, null, false);
    }

    public static CreateTaskResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false);
    }

    public static CreateTaskResult NotFound()
    {
        return new(null, null, true);
    }
}

public sealed record ChangeTaskStateResult(CreateTaskOutput? Task, bool TaskNotFound)
{
    public static ChangeTaskStateResult Success(CreateTaskOutput task)
    {
        return new(task, false);
    }

    public static ChangeTaskStateResult NotFound()
    {
        return new(null, true);
    }
}

public sealed class CreateProjectApplicationService(IProjectRepository projectRepository, TimeProvider timeProvider)
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

public sealed class CreateTaskApplicationService(IProjectRepository projectRepository, TimeProvider timeProvider)
{
    public CreateTaskResult Create(Guid projectId, string? rawTitle)
    {
        if (!TaskTitle.TryCreate(rawTitle, out var taskTitle))
        {
            return CreateTaskResult.Invalid("title", "任務標題為必填欄位");
        }

        var task = projectRepository.CreateTask(projectId, taskTitle!, timeProvider.GetUtcNow());

        return task is null
            ? CreateTaskResult.NotFound()
            : CreateTaskResult.Success(CoreFlowCommandOutputFactory.CreateTask(task));
    }
}

public sealed class ChangeTaskStateApplicationService(IProjectRepository projectRepository, TimeProvider timeProvider)
{
    public ChangeTaskStateResult Change(Guid projectId, Guid taskId, string stateKey)
    {
        var task = projectRepository.ChangeTaskState(projectId, taskId, stateKey, timeProvider.GetUtcNow());

        return task is null
            ? ChangeTaskStateResult.NotFound()
            : ChangeTaskStateResult.Success(CoreFlowCommandOutputFactory.CreateTask(task));
    }
}