using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class TaskApiIntegrationTests : ApiIntegrationTestBase
{
    public sealed record LifecycleTaskListResponse(IReadOnlyList<LifecycleTaskListItemResponse> Items);

    public sealed record LifecycleTaskListItemResponse(
        Guid Id,
        Guid ProjectId,
        string ProjectName,
        string Title,
        WorkflowStateResponse OriginalState,
        DateTimeOffset ChangedAt);

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
    public async Task ChangeTaskState_ToAnotherWorkflowState_UpdatesBoardAndDoesNotSetCompletedAt()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var changeResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/state",
            new ChangeTaskStateRequest("active"));

        Assert.That(changeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var changedTask = await changeResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(changedTask, Is.Not.Null);
        Assert.That(changedTask!.CurrentState.Key, Is.EqualTo("active"));
        Assert.That(changedTask.CurrentState.Label, Is.EqualTo("進行中"));
        Assert.That(changedTask.CompletedAt, Is.Null);
        Assert.That(changedTask.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(changedTask.ActivityTimeline.Select(item => item.Type), Does.Not.Contain("TaskCompleted"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);

        var activeColumn = board!.Columns.Single(column => column.StateKey == "active");
        var todoColumn = board.Columns.Single(column => column.StateKey == "todo");

        Assert.That(activeColumn.Tasks.Select(card => card.Id), Does.Contain(createdTask.Id));
        Assert.That(todoColumn.Tasks.Select(card => card.Id), Does.Not.Contain(createdTask.Id));

        var detailResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.CurrentState.Key, Is.EqualTo("active"));
        Assert.That(detail.CompletedAt, Is.Null);
        Assert.That(detail.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(detail.ActivityTimeline.Select(item => item.Type), Does.Not.Contain("TaskCompleted"));
    }

    [Test]
    public async Task ChangeTaskState_ToDoneFromActive_UpdatesBoardAndTaskDetail()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var moveToActiveResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/state",
            new ChangeTaskStateRequest("active"));

        Assert.That(moveToActiveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var changeResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/state",
            new ChangeTaskStateRequest("done"));

        Assert.That(changeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var changedTask = await changeResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(changedTask, Is.Not.Null);
        Assert.That(changedTask!.CurrentState.Key, Is.EqualTo("done"));
        Assert.That(changedTask.CurrentState.Label, Is.EqualTo("已完成"));
        Assert.That(changedTask.CompletedAt, Is.Not.Null);
        Assert.That(changedTask.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(changedTask.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskCompleted"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);

        var doneColumn = board!.Columns.Single(column => column.StateKey == "done");
        var activeColumn = board.Columns.Single(column => column.StateKey == "active");

        Assert.That(doneColumn.Tasks.Select(card => card.Id), Does.Contain(createdTask.Id));
        Assert.That(activeColumn.Tasks.Select(card => card.Id), Does.Not.Contain(createdTask.Id));

        var detailResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.CurrentState.Key, Is.EqualTo("done"));
        Assert.That(detail.CompletedAt, Is.Not.Null);
        Assert.That(detail.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskStateChanged"));
        Assert.That(detail.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskCompleted"));
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
    public async Task ChangeTaskState_WhenStateKeyDoesNotExist_ReturnsValidationError()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var response = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/state",
            new ChangeTaskStateRequest("missing-state"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(response);

        Assert.That(errors, Does.ContainKey("stateKey"));
    }

    [Test]
    public async Task ReorderTask_WhenTargetTaskIdIsMissing_ReturnsValidationError()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var response = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/order",
            new ReorderTaskRequest(null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(response);

        Assert.That(errors, Does.ContainKey("targetTaskId"));
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

    [Test]
    public async Task ArchiveTask_RemovesTaskFromBoardAndAddsItToArchivedList()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var archiveResponse = await Client.PatchAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/archive",
            content: null);

        Assert.That(archiveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var archivedTask = await archiveResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(archivedTask, Is.Not.Null);
        Assert.That(archivedTask!.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(archivedTask.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskArchived"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);
        Assert.That(board!.Columns.SelectMany(column => column.Tasks).Select(task => task.Id), Does.Not.Contain(createdTask.Id));

        var archivedListResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/archived");
        var archivedList = await archivedListResponse.Content.ReadFromJsonAsync<LifecycleTaskListResponse>();

        Assert.That(archivedListResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(archivedList, Is.Not.Null);
        Assert.That(archivedList!.Items.Select(item => item.Id), Does.Contain(createdTask.Id));

        var archivedItem = archivedList.Items.Single(item => item.Id == createdTask.Id);
        Assert.That(archivedItem.ProjectId, Is.EqualTo(project.Id));
        Assert.That(archivedItem.ProjectName, Is.EqualTo(project.Name));
        Assert.That(archivedItem.Title, Is.EqualTo("Build Kanban Board"));
        Assert.That(archivedItem.OriginalState.Key, Is.EqualTo("todo"));
    }

    [Test]
    public async Task RestoreArchivedTask_ReturnsTaskToBoardAndRemovesItFromArchivedList()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var firstTask = await CreateTaskAsync(project.Id, "Task A");
        var archivedTask = await CreateTaskAsync(project.Id, "Task B");

        var archiveResponse = await Client.PatchAsync(
            $"/api/projects/{project.Id}/tasks/{archivedTask.Id}/archive",
            content: null);

        Assert.That(archiveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var restoreResponse = await Client.PatchAsync(
            $"/api/projects/{project.Id}/tasks/{archivedTask.Id}/restore-from-archive",
            content: null);

        Assert.That(restoreResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var restoredTask = await restoreResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(restoredTask, Is.Not.Null);
        Assert.That(restoredTask!.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(restoredTask.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskRestoredFromArchive"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);

        var todoTaskIds = board!.Columns
            .Single(column => column.StateKey == "todo")
            .Tasks
            .Select(task => task.Id)
            .ToArray();

        Assert.That(todoTaskIds, Is.EqualTo(new[] { firstTask.Id, archivedTask.Id }));

        var archivedListResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/archived");
        var archivedList = await archivedListResponse.Content.ReadFromJsonAsync<LifecycleTaskListResponse>();

        Assert.That(archivedListResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(archivedList, Is.Not.Null);
        Assert.That(archivedList!.Items.Select(item => item.Id), Does.Not.Contain(archivedTask.Id));
    }

    [Test]
    public async Task MoveTaskToTrash_RemovesTaskFromBoardAndAddsItToTrashList()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var trashResponse = await Client.PatchAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/trash",
            content: null);

        Assert.That(trashResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var trashedTask = await trashResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(trashedTask, Is.Not.Null);
        Assert.That(trashedTask!.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(trashedTask.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskMovedToTrash"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);
        Assert.That(board!.Columns.SelectMany(column => column.Tasks).Select(task => task.Id), Does.Not.Contain(createdTask.Id));

        var trashListResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/trashed");
        var trashList = await trashListResponse.Content.ReadFromJsonAsync<LifecycleTaskListResponse>();

        Assert.That(trashListResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(trashList, Is.Not.Null);
        Assert.That(trashList!.Items.Select(item => item.Id), Does.Contain(createdTask.Id));

        var trashedItem = trashList.Items.Single(item => item.Id == createdTask.Id);
        Assert.That(trashedItem.ProjectId, Is.EqualTo(project.Id));
        Assert.That(trashedItem.ProjectName, Is.EqualTo(project.Name));
        Assert.That(trashedItem.Title, Is.EqualTo("Build Kanban Board"));
        Assert.That(trashedItem.OriginalState.Key, Is.EqualTo("todo"));
    }

    [Test]
    public async Task RestoreTrashedTask_ReturnsTaskToBoardAndRemovesItFromTrashList()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var firstTask = await CreateTaskAsync(project.Id, "Task A");
        var trashedTask = await CreateTaskAsync(project.Id, "Task B");

        var trashResponse = await Client.PatchAsync(
            $"/api/projects/{project.Id}/tasks/{trashedTask.Id}/trash",
            content: null);

        Assert.That(trashResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var restoreResponse = await Client.PatchAsync(
            $"/api/projects/{project.Id}/tasks/{trashedTask.Id}/restore-from-trash",
            content: null);

        Assert.That(restoreResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var restoredTask = await restoreResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(restoredTask, Is.Not.Null);
        Assert.That(restoredTask!.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(restoredTask.ActivityTimeline.Select(item => item.Type), Does.Contain("TaskRestoredFromTrash"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);

        var todoTaskIds = board!.Columns
            .Single(column => column.StateKey == "todo")
            .Tasks
            .Select(task => task.Id)
            .ToArray();

        Assert.That(todoTaskIds, Is.EqualTo(new[] { firstTask.Id, trashedTask.Id }));

        var trashListResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/trashed");
        var trashList = await trashListResponse.Content.ReadFromJsonAsync<LifecycleTaskListResponse>();

        Assert.That(trashListResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(trashList, Is.Not.Null);
        Assert.That(trashList!.Items.Select(item => item.Id), Does.Not.Contain(trashedTask.Id));
    }
}