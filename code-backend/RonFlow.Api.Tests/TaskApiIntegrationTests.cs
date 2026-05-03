using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class TaskApiIntegrationTests : ApiIntegrationTestBase
{
    [Test]
    public async Task CreateTask_WithBlankTitle_ReturnsValidationError()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var response = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateTaskRequest("  "));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(response);

        Assert.That(errors, Does.ContainKey("title"));
        Assert.That(errors["title"], Does.Contain("任務標題為必填欄位"));
    }

    [Test]
    public async Task CreateTask_WithValidTitle_AddsTaskToInitialWorkflowState()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Build Kanban Board"));

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var task = await createResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(task, Is.Not.Null);
        Assert.That(task!.ProjectId, Is.EqualTo(project.Id));
        Assert.That(task.Title, Is.EqualTo("Build Kanban Board"));
        Assert.That(task.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(task.CurrentState.Label, Is.EqualTo("待處理"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);

        var todoColumn = board!.Columns.Single(column => column.StateKey == "todo");

        Assert.That(todoColumn.Tasks.Select(card => card.Title), Does.Contain("Build Kanban Board"));
        Assert.That(board.Columns.Where(column => column.StateKey != "todo").All(column => column.Tasks.Count == 0), Is.True);
    }

    [Test]
    public async Task GetTaskDetail_ForCreatedTask_ReturnsCoreTaskInformation()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var response = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var task = await response.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(task, Is.Not.Null);
        Assert.That(task!.Id, Is.EqualTo(createdTask.Id));
        Assert.That(task.Title, Is.EqualTo("Build Kanban Board"));
        Assert.That(task.CurrentState.Label, Is.EqualTo("待處理"));
        Assert.That(task.CompletedAt, Is.Null);
        Assert.That(task.ActivityTimeline.Select(item => item.Message), Does.Contain("已建立任務"));
    }

    [Test]
    public async Task ChangeTaskState_ToDone_UpdatesBoardAndTaskDetail()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var changeResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/state",
            new ChangeTaskStateRequest("done"));

        Assert.That(changeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var changedTask = await changeResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(changedTask, Is.Not.Null);
        Assert.That(changedTask!.CurrentState.Key, Is.EqualTo("done"));
        Assert.That(changedTask.CurrentState.Label, Is.EqualTo("已完成"));
        Assert.That(changedTask.CompletedAt, Is.Not.Null);
        Assert.That(changedTask.ActivityTimeline.Select(item => item.Message), Does.Contain("已完成任務"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);

        var doneColumn = board!.Columns.Single(column => column.StateKey == "done");
        var todoColumn = board.Columns.Single(column => column.StateKey == "todo");

        Assert.That(doneColumn.Tasks.Select(card => card.Id), Does.Contain(createdTask.Id));
        Assert.That(todoColumn.Tasks.Select(card => card.Id), Does.Not.Contain(createdTask.Id));

        var detailResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.CurrentState.Key, Is.EqualTo("done"));
        Assert.That(detail.CompletedAt, Is.Not.Null);
        Assert.That(detail.ActivityTimeline.Select(item => item.Message), Does.Contain("已完成任務"));
    }

    [Test]
    public async Task ChangeTaskState_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var response = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{Guid.NewGuid()}/state",
            new ChangeTaskStateRequest("done"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateTask_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/tasks",
            new CreateTaskRequest("Build Kanban Board"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetTaskDetail_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var response = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}