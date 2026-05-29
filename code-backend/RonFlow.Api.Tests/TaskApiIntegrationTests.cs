using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class TaskApiIntegrationTests : ApiIntegrationTestBase
{
    public sealed record ProjectInvitationResponse(Guid Id, string Invitee, string Status);

    public sealed record TaskSubtaskResponse(Guid Id, string Title, bool IsChecked, int Order);

    public sealed record ChecklistTaskDetailResponse(
        Guid Id,
        Guid ProjectId,
        string Title,
        WorkflowStateResponse CurrentState,
        DateTimeOffset? CompletedAt,
        IReadOnlyList<TaskSubtaskResponse> Subtasks);

    public sealed record LifecycleTaskListResponse(IReadOnlyList<LifecycleTaskListItemResponse> Items);

    public sealed record LifecycleTaskListItemResponse(
        Guid Id,
        Guid ProjectId,
        string ProjectName,
        string Title,
        WorkflowStateResponse OriginalState,
        DateTimeOffset ChangedAt);

    [Test]
    public async Task CreateTask_WhenAnonymousUser_ReturnsUnauthorized()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var anonymousClient = CreateAnonymousClient();

        var response = await anonymousClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Build Kanban Board"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

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
        Assert.That(task.LifecycleState, Is.EqualTo("activeRecord"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);

        var todoColumn = board!.Columns.Single(column => column.StateKey == "todo");

        Assert.That(todoColumn.Tasks.Select(card => card.Title), Does.Contain("Build Kanban Board"));
        Assert.That(board.Columns.Where(column => column.StateKey != "todo").All(column => column.Tasks.Count == 0), Is.True);
    }

    [Test]
    public async Task CreateTask_WhenProjectHasSubtaskTemplates_InheritsUncheckedSubtasks()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var templateResponse = await Client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/subtask-templates",
            new
            {
                items = new[]
                {
                    new { title = "需求已釐清" },
                    new { title = "驗收測試已撰寫" },
                },
            });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Build Kanban Board"));

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var task = await createResponse.Content.ReadFromJsonAsync<ChecklistTaskDetailResponse>();

        Assert.That(task, Is.Not.Null);
        Assert.That(task!.CurrentState.Key, Is.EqualTo("todo"));
        Assert.That(task.Subtasks.Select(item => item.Title), Is.EqualTo(new[] { "需求已釐清", "驗收測試已撰寫" }));
        Assert.That(task.Subtasks.Select(item => item.Order), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(task.Subtasks.All(item => item.IsChecked is false), Is.True);
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
        Assert.That(task.LifecycleState, Is.EqualTo("activeRecord"));
        Assert.That(task.CompletedAt, Is.Null);
        Assert.That(task.ActivityTimeline.Select(item => item.Message), Does.Contain("已建立任務"));
        Assert.That(task.CanEnterEdit, Is.True);
    }

    [Test]
    public async Task GetTaskDetail_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-task-detail-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-task-detail-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");
        var createdTask = await CreateTaskAsync(firstSessionClient, project.Id, "Build Kanban Board");

        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task GetTaskDetail_WhenAnotherUserHoldsContentEditLock_ReturnsCanEnterEditFalseAndAcquireReturnsConflict()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");
        using var memberClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(memberClient);

        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        var acceptResponse = await memberClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", content: null);
        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var acquireResponse = await Client.PostAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock", content: null);
        Assert.That(acquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var memberDetailResponse = await memberClient.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var memberTask = await memberDetailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(memberDetailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(memberTask, Is.Not.Null);
        Assert.That(memberTask!.CanEnterEdit, Is.False);

        var memberAcquireResponse = await memberClient.PostAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock", content: null);
        Assert.That(memberAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task ContentEditLock_WhenOwnerReleasesLock_OtherMemberCanEnterEditAndAcquireSuccessfully()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");
        using var memberClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(memberClient);

        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        var acceptResponse = await memberClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", content: null);
        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ownerAcquireResponse = await Client.PostAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock", content: null);
        Assert.That(ownerAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ownerReleaseResponse = await Client.DeleteAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock");
        Assert.That(ownerReleaseResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var memberDetailResponse = await memberClient.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var memberTask = await memberDetailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(memberDetailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(memberTask, Is.Not.Null);
        Assert.That(memberTask!.CanEnterEdit, Is.True);

        var memberAcquireResponse = await memberClient.PostAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock", content: null);
        Assert.That(memberAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ContentEditLock_WhenNewRonFlowSessionForSameUserBecomesActive_ReleasesOldSessionLock()
    {
        using var firstSessionClient = CreateAuthenticatedClient(
            TestUser.OwnerA,
            new Claim("ronflow_session_id", "owner-a-session-1"));
        using var secondSessionClient = CreateAuthenticatedClient(
            TestUser.OwnerA,
            new Claim("ronflow_session_id", "owner-a-session-2"));
        using var memberClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);
        await EnsureKnownUserAsync(memberClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");
        var createdTask = await CreateTaskAsync(firstSessionClient, project.Id, "Build Kanban Board");

        var invitationResponse = await firstSessionClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        var acceptResponse = await memberClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", content: null);
        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var firstAcquireResponse = await firstSessionClient.PostAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock",
            content: null);

        var firstSessionActivationResponse = await firstSessionClient.PostAsync("/api/session/activate", content: null);
        Assert.That(firstSessionActivationResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        firstAcquireResponse = await firstSessionClient.PostAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock",
            content: null);

        Assert.That(firstAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var secondSessionActivationResponse = await secondSessionClient.PostAsync("/api/session/activate", content: null);
        Assert.That(secondSessionActivationResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var memberDetailResponse = await memberClient.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var memberTask = await memberDetailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(memberDetailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(memberTask, Is.Not.Null);
        Assert.That(memberTask!.CanEnterEdit, Is.True);

        var memberAcquireResponse = await memberClient.PostAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock",
            content: null);

        Assert.That(memberAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ContentEditLock_WhenOwnerSwitchesToAnotherProject_ReleasesOldProjectLockForOtherMember()
    {
        using var ownerSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-project-switch-lock-session");
        using var memberClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(ownerSessionClient);
        await EnsureKnownUserAsync(memberClient);
        await ActivateSessionAsync(ownerSessionClient);

        var sourceProject = await CreateProjectAsync(ownerSessionClient, "Lock Source Project");
        var targetProject = await CreateProjectAsync(ownerSessionClient, "Lock Target Project");
        var createdTask = await CreateTaskAsync(ownerSessionClient, sourceProject.Id, "Build Kanban Board");

        var invitationResponse = await ownerSessionClient.PostAsJsonAsync(
            $"/api/projects/{sourceProject.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        var acceptResponse = await memberClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", content: null);
        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var sourceBoardResponse = await ownerSessionClient.GetAsync($"/api/projects/{sourceProject.Id}/board");
        Assert.That(sourceBoardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ownerAcquireResponse = await ownerSessionClient.PostAsync(
            $"/api/projects/{sourceProject.Id}/tasks/{createdTask.Id}/content-edit-lock",
            content: null);
        Assert.That(ownerAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var memberDetailBeforeSwitchResponse = await memberClient.GetAsync($"/api/projects/{sourceProject.Id}/tasks/{createdTask.Id}");
        var memberTaskBeforeSwitch = await memberDetailBeforeSwitchResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(memberDetailBeforeSwitchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(memberTaskBeforeSwitch, Is.Not.Null);
        Assert.That(memberTaskBeforeSwitch!.CanEnterEdit, Is.False);

        await ReleaseProjectScopeAsync(ownerSessionClient);

        var targetBoardResponse = await ownerSessionClient.GetAsync($"/api/projects/{targetProject.Id}/board");
        Assert.That(targetBoardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var memberDetailAfterSwitchResponse = await memberClient.GetAsync($"/api/projects/{sourceProject.Id}/tasks/{createdTask.Id}");
        var memberTaskAfterSwitch = await memberDetailAfterSwitchResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();

        Assert.That(memberDetailAfterSwitchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(memberTaskAfterSwitch, Is.Not.Null);
        Assert.That(memberTaskAfterSwitch!.CanEnterEdit, Is.True);

        var memberAcquireResponse = await memberClient.PostAsync(
            $"/api/projects/{sourceProject.Id}/tasks/{createdTask.Id}/content-edit-lock",
            content: null);

        Assert.That(memberAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task AcquireContentEditLock_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-lock-acquire-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-lock-acquire-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");
        var createdTask = await CreateTaskAsync(firstSessionClient, project.Id, "Build Kanban Board");

        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.PostAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock",
            content: null);

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task ReleaseContentEditLock_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-lock-release-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-lock-release-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");
        var createdTask = await CreateTaskAsync(firstSessionClient, project.Id, "Build Kanban Board");

        await ActivateSessionAsync(firstSessionClient);

        var acquireResponse = await firstSessionClient.PostAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock",
            content: null);
        Assert.That(acquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.DeleteAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock");

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task UpdateTask_WhenCallerHasNotAcquiredContentEditLock_ReturnsConflict()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var response = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}",
            new UpdateTaskRequest("Updated Title", "Updated Description", new DateOnly(2026, 5, 20)));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task ChangeTaskState_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-task-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-task-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");
        var createdTask = await CreateTaskAsync(firstSessionClient, project.Id, "Build Kanban Board");

        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/state",
            new ChangeTaskStateRequest("active"));

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task ArchiveTask_WhenAnotherUserHoldsContentEditLock_ReturnsConflict()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");
        using var memberClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(memberClient);

        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        var acceptResponse = await memberClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", content: null);
        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ownerAcquireResponse = await Client.PostAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock", content: null);
        Assert.That(ownerAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var archiveResponse = await memberClient.PatchAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/archive", content: null);

        Assert.That(archiveResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
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
    public async Task ReplaceTaskSubtasks_WhenAllItemsAreChecked_AutoMovesTaskToReviewButNotDone()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var templateResponse = await Client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/subtask-templates",
            new
            {
                items = new[]
                {
                    new { title = "需求已釐清" },
                    new { title = "已部署到 localhost" },
                },
            });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var detailResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ChecklistTaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);

        var replaceResponse = await Client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/subtasks",
            new
            {
                items = detail!.Subtasks.Select(item => new
                {
                    id = item.Id,
                    title = item.Title,
                    isChecked = true,
                    order = item.Order,
                }).ToArray(),
            });

        Assert.That(replaceResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updatedTask = await replaceResponse.Content.ReadFromJsonAsync<ChecklistTaskDetailResponse>();

        Assert.That(updatedTask, Is.Not.Null);
        Assert.That(updatedTask!.CurrentState.Key, Is.EqualTo("review"));
        Assert.That(updatedTask.CompletedAt, Is.Null);
        Assert.That(updatedTask.Subtasks.All(item => item.IsChecked), Is.True);

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(board, Is.Not.Null);
        Assert.That(board!.Columns.Single(column => column.StateKey == "review").Tasks.Select(task => task.Id), Does.Contain(createdTask.Id));
        Assert.That(board.Columns.Single(column => column.StateKey == "done").Tasks.Select(task => task.Id), Does.Not.Contain(createdTask.Id));
    }

    [Test]
    public async Task ReplaceTaskSubtasks_WhenAnotherUserHoldsContentEditLock_ReturnsConflict()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");
        using var memberClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(memberClient);

        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        var acceptResponse = await memberClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", content: null);
        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ownerAcquireResponse = await Client.PostAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}/content-edit-lock", content: null);
        Assert.That(ownerAcquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var detailResponse = await memberClient.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ChecklistTaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);

        var replaceResponse = await memberClient.PutAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/subtasks",
            new
            {
                items = detail!.Subtasks.Select(item => new
                {
                    id = item.Id,
                    title = item.Title,
                    isChecked = !item.IsChecked,
                    order = item.Order,
                }).ToArray(),
            });

        Assert.That(replaceResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
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
    public async Task GetTaskDetail_WhenTaskBelongsToAnotherUser_ReturnsAccessDenied()
    {
        using var outsiderClient = CreateAuthenticatedClient(TestUser.OwnerB);
        var project = await CreateProjectAsync(Client, "Owner A Project");
        var createdTask = await CreateTaskAsync(Client, project.Id, "Build Kanban Board");

        var response = await outsiderClient.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");

        await AssertAccessDeniedAsync(response);
    }

    [Test]
    public async Task ChangeTaskState_WhenTaskBelongsToAnotherUser_ReturnsAccessDenied()
    {
        using var outsiderClient = CreateAuthenticatedClient(TestUser.OwnerB);
        var project = await CreateProjectAsync(Client, "Owner A Project");
        var createdTask = await CreateTaskAsync(Client, project.Id, "Build Kanban Board");

        var response = await outsiderClient.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{createdTask.Id}/state",
            new ChangeTaskStateRequest("active"));

        await AssertAccessDeniedAsync(response);
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
        Assert.That(archivedTask.LifecycleState, Is.EqualTo("archived"));
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
        Assert.That(restoredTask.LifecycleState, Is.EqualTo("activeRecord"));
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
        Assert.That(trashedTask.LifecycleState, Is.EqualTo("trashed"));
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
        Assert.That(restoredTask.LifecycleState, Is.EqualTo("activeRecord"));
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