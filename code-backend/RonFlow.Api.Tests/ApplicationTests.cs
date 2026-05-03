using System.Net;
using RonFlow.Application;
using RonFlow.Domain;

namespace RonFlow.Api.Tests;

public sealed class CreateProjectApplicationServiceTests
{
    [Test]
    public void Create_WithBlankName_ReturnsValidationError()
    {
        var applicationService = new CreateProjectApplicationService(new TestProjectRepository(), new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = applicationService.Create("   ");

        Assert.That(result.Project, Is.Null);
        Assert.That(result.ValidationError, Is.Not.Null);
        Assert.That(result.ValidationError!.Field, Is.EqualTo("name"));
        Assert.That(result.ValidationError.Message, Is.EqualTo("專案名稱為必填欄位"));
    }

    [Test]
    public void Create_WithValidName_PersistsProjectAndReturnsOutput()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var repository = new TestProjectRepository();
        var applicationService = new CreateProjectApplicationService(repository, new FixedTimeProvider(createdAt));

        var result = applicationService.Create("RonFlow Project");

        Assert.That(result.ValidationError, Is.Null);
        Assert.That(result.Project, Is.Not.Null);
        Assert.That(result.Project!.Name, Is.EqualTo("RonFlow Project"));
        Assert.That(result.Project.WorkflowStates.Select(state => state.Key), Is.EqualTo(new[] { "todo", "active", "review", "done" }));
        Assert.That(repository.GetProjects().Select(project => project.Name), Does.Contain("RonFlow Project"));
    }
}

public sealed class CreateTaskApplicationServiceTests
{
    [Test]
    public void Create_WithBlankTitle_ReturnsValidationError()
    {
        var repository = new TestProjectRepository();
        var applicationService = new CreateTaskApplicationService(repository, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = applicationService.Create(Guid.NewGuid(), "   ");

        Assert.That(result.Task, Is.Null);
        Assert.That(result.ValidationError, Is.Not.Null);
        Assert.That(result.ValidationError!.Field, Is.EqualTo("title"));
        Assert.That(result.ValidationError.Message, Is.EqualTo("任務標題為必填欄位"));
        Assert.That(result.ProjectNotFound, Is.False);
    }

    [Test]
    public void Create_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var repository = new TestProjectRepository();
        var applicationService = new CreateTaskApplicationService(repository, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = applicationService.Create(Guid.NewGuid(), "Build Kanban Board");

        Assert.That(result.Task, Is.Null);
        Assert.That(result.ValidationError, Is.Null);
        Assert.That(result.ProjectNotFound, Is.True);
    }

    [Test]
    public void Create_WithValidTitle_ReturnsCreatedTaskOutput()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var repository = new TestProjectRepository();
        var project = Project.Create(TestProjectData.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);

        var applicationService = new CreateTaskApplicationService(repository, new FixedTimeProvider(createdAt.AddMinutes(5)));

        var result = applicationService.Create(project.Id, "Build Kanban Board");

        Assert.That(result.ValidationError, Is.Null);
        Assert.That(result.ProjectNotFound, Is.False);
        Assert.That(result.Task, Is.Not.Null);
        Assert.That(result.Task!.Title, Is.EqualTo("Build Kanban Board"));
        Assert.That(result.Task.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(result.Task.ActivityTimeline.Select(item => item.Message), Does.Contain("已建立任務"));
    }
}

public sealed class ChangeTaskStateApplicationServiceTests
{
    [Test]
    public void Change_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var repository = new TestProjectRepository();
        var applicationService = new ChangeTaskStateApplicationService(repository, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = applicationService.Change(Guid.NewGuid(), Guid.NewGuid(), "done");

        Assert.That(result.Task, Is.Null);
        Assert.That(result.TaskNotFound, Is.True);
    }

    [Test]
    public void Change_ToAnotherWorkflowState_UpdatesStateWithoutCompletedAt()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var changedAt = createdAt.AddMinutes(15);
        var repository = new TestProjectRepository();
        var project = Project.Create(TestProjectData.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);
        var task = project.CreateTask(TestProjectData.CreateTaskTitle("Build Kanban Board"), createdAt.AddMinutes(5));

        var applicationService = new ChangeTaskStateApplicationService(repository, new FixedTimeProvider(changedAt));

        var result = applicationService.Change(project.Id, task.Id, "active");

        Assert.That(result.TaskNotFound, Is.False);
        Assert.That(result.Task, Is.Not.Null);
        Assert.That(result.Task!.CurrentState.Key, Is.EqualTo("active"));
        Assert.That(result.Task.CompletedAt, Is.Null);
        Assert.That(result.Task.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(result.Task.ActivityTimeline.Select(item => item.Type), Does.Not.Contain("TaskCompleted"));
    }

    [Test]
    public void Change_ToDoneFromActive_UpdatesStateAndCompletedAt()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var movedToActiveAt = createdAt.AddMinutes(15);
        var completedAt = createdAt.AddMinutes(30);
        var repository = new TestProjectRepository();
        var project = Project.Create(TestProjectData.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);
        var task = project.CreateTask(TestProjectData.CreateTaskTitle("Build Kanban Board"), createdAt.AddMinutes(5));

        var moveToActiveService = new ChangeTaskStateApplicationService(repository, new FixedTimeProvider(movedToActiveAt));
        var moveToActiveResult = moveToActiveService.Change(project.Id, task.Id, "active");

        Assert.That(moveToActiveResult.TaskNotFound, Is.False);

        var completeService = new ChangeTaskStateApplicationService(repository, new FixedTimeProvider(completedAt));
        var result = completeService.Change(project.Id, task.Id, "done");

        Assert.That(result.TaskNotFound, Is.False);
        Assert.That(result.Task, Is.Not.Null);
        Assert.That(result.Task!.CurrentState.Key, Is.EqualTo("done"));
        Assert.That(result.Task.CompletedAt, Is.EqualTo(completedAt));
        Assert.That(result.Task.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(result.Task.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskCompleted"));
        Assert.That(result.Task.ActivityTimeline.Select(item => item.Message), Does.Contain("已完成任務"));
    }
}

public sealed class CoreFlowQueryServiceTests
{
    [Test]
    public void GetProjectBoard_WhenProjectDoesNotExist_ReturnsNull()
    {
        var queryService = new GetProjectBoardQueryService(new TestProjectRepository());

        var board = queryService.Get(Guid.NewGuid());

        Assert.That(board, Is.Null);
    }

    [Test]
    public void GetTaskDetail_WhenTaskDoesNotExist_ReturnsNull()
    {
        var repository = new TestProjectRepository();
        var project = Project.Create(TestProjectData.CreateProjectName("RonFlow Project"), DateTimeOffset.UtcNow, DefaultWorkflow.CreateStates());
        repository.Add(project);
        var queryService = new GetTaskDetailQueryService(repository);

        var task = queryService.Get(project.Id, Guid.NewGuid());

        Assert.That(task, Is.Null);
    }
}

internal static class TestProjectData
{
    public static ProjectName CreateProjectName(string value)
    {
        var isValid = ProjectName.TryCreate(value, out var projectName);

        Assert.That(isValid, Is.True);
        Assert.That(projectName, Is.Not.Null);

        return projectName!;
    }

    public static TaskTitle CreateTaskTitle(string value)
    {
        var isValid = TaskTitle.TryCreate(value, out var taskTitle);

        Assert.That(isValid, Is.True);
        Assert.That(taskTitle, Is.Not.Null);

        return taskTitle!;
    }
}

internal sealed class TestProjectRepository : IProjectRepository
{
    private readonly Dictionary<Guid, Project> projects = [];

    public IReadOnlyList<ProjectSummaryModel> GetProjects()
    {
        return projects.Values
            .OrderByDescending(project => project.UpdatedAt)
            .Select(project => project.ToSummaryModel())
            .ToArray();
    }

    public void Add(Project project)
    {
        projects.Add(project.Id, project);
    }

    public ProjectBoardModel? GetBoard(Guid projectId)
    {
        return projects.TryGetValue(projectId, out var project)
            ? project.ToBoardModel()
            : null;
    }

    public TaskModel? CreateTask(Guid projectId, TaskTitle title, DateTimeOffset createdAt)
    {
        if (!projects.TryGetValue(projectId, out var project))
        {
            return null;
        }

        return project.CreateTask(title, createdAt).ToModel();
    }

    public TaskModel? GetTask(Guid projectId, Guid taskId)
    {
        return projects.TryGetValue(projectId, out var project)
            ? project.GetTask(taskId)?.ToModel()
            : null;
    }

    public TaskModel? ChangeTaskState(Guid projectId, Guid taskId, string stateKey, DateTimeOffset changedAt)
    {
        if (!projects.TryGetValue(projectId, out var project))
        {
            return null;
        }

        return project.ChangeTaskState(taskId, stateKey, changedAt)?.ToModel();
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}