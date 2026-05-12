using RonFlow.Domain;

namespace RonFlow.Api.Tests;

public sealed class ProjectDomainTests
{
    [Test]
    public void Create_WithValidName_StartsWithDefaultWorkflow()
    {
        var createdAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var projectName = CreateProjectName("RonFlow Project");

        var project = Project.Create(projectName, createdAt, DefaultWorkflow.CreateStates());
        var projectModel = project.ToModel();

        Assert.That(projectModel.Name, Is.EqualTo("RonFlow Project"));
        Assert.That(projectModel.UpdatedAt, Is.EqualTo(createdAt));
        Assert.That(projectModel.WorkflowStates.Select(state => state.Key), Is.EqualTo(new[] { "todo", "active", "review", "done" }));
        Assert.That(projectModel.WorkflowStates.Single(state => state.IsInitialState).Key, Is.EqualTo("todo"));
    }

    [Test]
    public void Touch_WhenTaskActivityOccurs_UpdatesProjectTimestampWithoutOwningTaskState()
    {
        var projectCreatedAt = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        var taskCreatedAt = new DateTimeOffset(2026, 5, 3, 9, 15, 0, TimeSpan.Zero);
        var project = Project.Create(CreateProjectName("RonFlow Project"), projectCreatedAt, DefaultWorkflow.CreateStates());
        var taskTitle = CreateTaskTitle("Build Kanban Board");

        var task = RonFlow.Domain.Task.Create(project.Id, taskTitle, project.GetDefaultWorkflowState(), taskCreatedAt);
        project.Touch(taskCreatedAt);
        var taskModel = task.ToModel();

        Assert.That(project.UpdatedAt, Is.EqualTo(taskCreatedAt));
        Assert.That(task.ProjectId, Is.EqualTo(project.Id));
        Assert.That(task.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Message), Does.Contain("已建立任務"));
        Assert.That(project.WorkflowStates.Select(state => state.Key), Is.EqualTo(new[] { "todo", "active", "review", "done" }));
    }

    [Test]
    public void ChangeState_ToAnotherWorkflowState_RecordsStateChangedWithoutCompletion()
    {
        var taskCreatedAt = new DateTimeOffset(2026, 5, 3, 9, 15, 0, TimeSpan.Zero);
        var changedAt = taskCreatedAt.AddMinutes(15);
        var workflowStates = DefaultWorkflow.CreateStates();
        var task = RonFlow.Domain.Task.Create(
            Guid.NewGuid(),
            CreateTaskTitle("Build Kanban Board"),
            workflowStates.Single(state => state.IsInitialState),
            taskCreatedAt);

        task.ChangeState(workflowStates.Single(state => state.Key == "active"), changedAt);
        var taskModel = task.ToModel();

        Assert.That(taskModel.CurrentState.Key, Is.EqualTo("active"));
        Assert.That(taskModel.CurrentState.Label, Is.EqualTo("進行中"));
        Assert.That(taskModel.CompletedAt, Is.Null);
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Type), Does.Not.Contain("TaskCompleted"));
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Message), Does.Contain("任務狀態已變更為 進行中"));
    }

    [Test]
    public void ChangeState_ToDoneFromActive_UpdatesCurrentStateAndRecordsCompletion()
    {
        var taskCreatedAt = new DateTimeOffset(2026, 5, 3, 9, 15, 0, TimeSpan.Zero);
        var movedToActiveAt = taskCreatedAt.AddMinutes(15);
        var completedAt = taskCreatedAt.AddMinutes(30);
        var workflowStates = DefaultWorkflow.CreateStates();
        var task = RonFlow.Domain.Task.Create(
            Guid.NewGuid(),
            CreateTaskTitle("Build Kanban Board"),
            workflowStates.Single(state => state.IsInitialState),
            taskCreatedAt);

        task.ChangeState(workflowStates.Single(state => state.Key == "active"), movedToActiveAt);
        task.ChangeState(workflowStates.Single(state => state.Key == "done"), completedAt);
        var taskModel = task.ToModel();

        Assert.That(taskModel.CurrentState.Key, Is.EqualTo("done"));
        Assert.That(taskModel.CurrentState.Label, Is.EqualTo("已完成"));
        Assert.That(taskModel.CompletedAt, Is.EqualTo(completedAt));
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskCompleted"));
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Message), Does.Contain("已完成任務"));
    }

    [Test]
    public void ChangeState_ToSameState_DoesNotAddNewActivity()
    {
        var taskCreatedAt = new DateTimeOffset(2026, 5, 3, 9, 15, 0, TimeSpan.Zero);
        var workflowStates = DefaultWorkflow.CreateStates();
        var initialState = workflowStates.Single(state => state.IsInitialState);
        var task = RonFlow.Domain.Task.Create(
            Guid.NewGuid(),
            CreateTaskTitle("Build Kanban Board"),
            initialState,
            taskCreatedAt);

        task.ChangeState(initialState, taskCreatedAt.AddMinutes(30));
        var taskModel = task.ToModel();

        Assert.That(taskModel.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(taskModel.CompletedAt, Is.Null);
        Assert.That(taskModel.ActivityTimeline.Select(item => item.Type), Is.EqualTo(new[] { "TaskCreated" }));
    }

    private static ProjectName CreateProjectName(string value)
    {
        var isValid = ProjectName.TryCreate(value, out var projectName);

        Assert.That(isValid, Is.True);
        Assert.That(projectName, Is.Not.Null);

        return projectName!;
    }

    private static TaskTitle CreateTaskTitle(string value)
    {
        var isValid = TaskTitle.TryCreate(value, out var taskTitle);

        Assert.That(isValid, Is.True);
        Assert.That(taskTitle, Is.Not.Null);

        return taskTitle!;
    }
}