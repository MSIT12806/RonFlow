using System.Net;
using RonFlow.Application;
using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Api.Tests;

public sealed class CreateProjectCommandServiceTests
{
    [Test]
    public void Create_WithBlankName_ReturnsValidationError()
    {
        var commandService = new CreateProjectCommandService(new TestProjectRepository(), new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = commandService.Create("   ");

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
        var commandService = new CreateProjectCommandService(repository, new FixedTimeProvider(createdAt));

        var result = commandService.Create("RonFlow Project");

        Assert.That(result.ValidationError, Is.Null);
        Assert.That(result.Project, Is.Not.Null);
        Assert.That(result.Project!.Name, Is.EqualTo("RonFlow Project"));
        Assert.That(result.Project.WorkflowStates.Select(state => state.Key), Is.EqualTo(new[] { "todo", "active", "review", "done" }));
        Assert.That(repository.GetProjects().Select(project => project.Name), Does.Contain("RonFlow Project"));
    }
}

public sealed class CreateTaskCommandServiceTests
{
    [Test]
    public void Create_WithBlankTitle_ReturnsValidationError()
    {
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var commandService = new CreateTaskCommandService(repository, taskRepository, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = commandService.Create(Guid.NewGuid(), "   ");

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
        var taskRepository = new TestTaskRepository();
        var commandService = new CreateTaskCommandService(repository, taskRepository, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = commandService.Create(Guid.NewGuid(), "Build Kanban Board");

        Assert.That(result.Task, Is.Null);
        Assert.That(result.ValidationError, Is.Null);
        Assert.That(result.ProjectNotFound, Is.True);
    }

    [Test]
    public void Create_WithValidTitle_ReturnsCreatedTaskOutput()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var project = Project.Create(TestObjectFactory.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);

        var createTaskCommandService = new CreateTaskCommandService(repository, taskRepository, new FixedTimeProvider(createdAt.AddMinutes(5)));

        var result = createTaskCommandService.Create(project.Id, "Build Kanban Board");

        Assert.That(result.ValidationError, Is.Null);
        Assert.That(result.ProjectNotFound, Is.False);
        Assert.That(result.Task, Is.Not.Null);
        Assert.That(result.Task!.Title, Is.EqualTo("Build Kanban Board"));
        Assert.That(result.Task.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(result.Task.ActivityTimeline.Select(item => item.Message), Does.Contain("已建立任務"));
        Assert.That(taskRepository.GetByProjectId(project.Id).Select(task => task.Title), Does.Contain("Build Kanban Board"));
    }
}

public sealed class ChangeTaskStateCommandServiceTests
{
    [Test]
    public void Change_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var commandService = new ChangeTaskStateCommandService(repository, taskRepository, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = commandService.Change(Guid.NewGuid(), Guid.NewGuid(), "done");

        Assert.That(result.Task, Is.Null);
        Assert.That(result.ValidationError, Is.Null);
        Assert.That(result.TaskNotFound, Is.True);
    }

    [Test]
    public void Change_WhenStateKeyDoesNotExist_ReturnsValidationError()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var project = Project.Create(TestObjectFactory.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);

        var task = DomainTask.Create(
            project.Id,
            TestObjectFactory.CreateTaskTitle("Build Kanban Board"),
            project.GetDefaultWorkflowState(),
            createdAt.AddMinutes(5),
            0);
        taskRepository.Add(task);

        var commandService = new ChangeTaskStateCommandService(repository, taskRepository, new FixedTimeProvider(createdAt.AddMinutes(15)));

        var result = commandService.Change(project.Id, task.Id, "missing-state");

        Assert.That(result.Task, Is.Null);
        Assert.That(result.TaskNotFound, Is.False);
        Assert.That(result.ValidationError, Is.Not.Null);
        Assert.That(result.ValidationError!.Field, Is.EqualTo("stateKey"));
    }

    [Test]
    public void Change_ToAnotherWorkflowState_UpdatesStateWithoutCompletedAt()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var changedAt = createdAt.AddMinutes(15);
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var project = Project.Create(TestObjectFactory.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);

        var task = DomainTask.Create(
            project.Id,
            TestObjectFactory.CreateTaskTitle("Build Kanban Board"),
            project.GetDefaultWorkflowState(),
            createdAt.AddMinutes(5),
            0);
        taskRepository.Add(task);

        var commandService = new ChangeTaskStateCommandService(repository, taskRepository, new FixedTimeProvider(changedAt));

        var result = commandService.Change(project.Id, task.Id, "active");

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
        var taskRepository = new TestTaskRepository();
        var project = Project.Create(TestObjectFactory.CreateProjectName("RonFlow Project"), createdAt, DefaultWorkflow.CreateStates());
        repository.Add(project);

        var task = DomainTask.Create(
            project.Id,
            TestObjectFactory.CreateTaskTitle("Build Kanban Board"),
            project.GetDefaultWorkflowState(),
            createdAt.AddMinutes(5),
            0);
        taskRepository.Add(task);

        var moveToActiveService = new ChangeTaskStateCommandService(repository, taskRepository, new FixedTimeProvider(movedToActiveAt));
        var moveToActiveResult = moveToActiveService.Change(project.Id, task.Id, "active");

        Assert.That(moveToActiveResult.TaskNotFound, Is.False);

        var completeService = new ChangeTaskStateCommandService(repository, taskRepository, new FixedTimeProvider(completedAt));
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
        var queryService = new GetProjectBoardQueryService(new TestCoreFlowReadStore(new TestProjectRepository(), new TestTaskRepository()));

        var board = queryService.Get(Guid.NewGuid());

        Assert.That(board, Is.Null);
    }

    [Test]
    public void GetTaskDetail_WhenTaskDoesNotExist_ReturnsNull()
    {
        var repository = new TestProjectRepository();
        var taskRepository = new TestTaskRepository();
        var project = Project.Create(TestObjectFactory.CreateProjectName("RonFlow Project"), DateTimeOffset.UtcNow, DefaultWorkflow.CreateStates());
        repository.Add(project);
        var queryService = new GetTaskDetailQueryService(new TestCoreFlowReadStore(repository, taskRepository));

        var task = queryService.Get(project.Id, Guid.NewGuid());

        Assert.That(task, Is.Null);
    }
}

internal sealed class TestCoreFlowReadStore(IProjectRepository projectRepository, ITaskRepository taskRepository) : ICoreFlowReadStore
{
    public IReadOnlyList<ProjectSummaryModel> GetProjects()
    {
        return projectRepository.GetProjects();
    }

    public ProjectBoardModel? GetProjectBoard(Guid projectId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return null;
        }

        return new ProjectBoardModel(
            project.Id,
            project.Name,
            project.WorkflowStates.Select(state => state.ToModel()).ToArray(),
            taskRepository.GetByProjectId(projectId)
                .Select(task => task.ToModel())
                .ToArray());
    }

    public TaskModel? GetTaskDetail(Guid projectId, Guid taskId)
    {
        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return null;
        }

        return task.ToModel();
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

    public Project? Get(Guid projectId)
    {
        return projects.GetValueOrDefault(projectId);
    }

    public void Update(Project project)
    {
        if (projects.ContainsKey(project.Id))
        {
            projects[project.Id] = project;
        }
    }
}

internal sealed class TestTaskRepository : ITaskRepository
{
    private readonly Dictionary<Guid, DomainTask> tasks = [];

    public DomainTask? Get(Guid taskId)
    {
        return tasks.GetValueOrDefault(taskId);
    }

    public IReadOnlyList<DomainTask> GetByProjectId(Guid projectId)
    {
        return tasks.Values
            .Where(task => task.ProjectId == projectId)
            .OrderBy(task => task.CreatedAt)
            .ToArray();
    }

    public void Add(DomainTask task)
    {
        tasks.Add(task.Id, task);
    }

    public void Update(DomainTask task)
    {
        if (tasks.ContainsKey(task.Id))
        {
            tasks[task.Id] = task;
        }
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}

internal static class TestObjectFactory
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