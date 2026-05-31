using System.Net;
using System.Net.Http.Json;
using System.Text;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class ProjectApiIntegrationTests : ApiIntegrationTestBase
{
    public sealed record ProjectSubtaskTemplateResponse(Guid Id, string Title, int Order);

    public sealed record ProjectSubtaskTemplateListResponse(IReadOnlyList<ProjectSubtaskTemplateResponse> Items);

    public sealed record ProjectCodeTraceabilityItemResponse(
        Guid TaskId,
        string TaskTitle,
        string Category,
        string ChangeType,
        string Target);

    public sealed record ProjectCodeTraceabilityResponse(IReadOnlyList<ProjectCodeTraceabilityItemResponse> Items);

    [Test]
    public async Task GetProjects_WhenAuthenticatedUserHasNoProjects_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/api/projects");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<ProjectListResponse>();

        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Items, Is.Empty);
    }

    [Test]
    public async Task GetProjects_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-project-read-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-project-read-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);
        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.GetAsync("/api/projects");

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task GetProjects_WhenAnonymousUser_ReturnsUnauthorized()
    {
        using var anonymousClient = CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/projects");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetProjects_WhenMultipleUsersHaveProjects_ReturnsOnlyCurrentUsersProjects()
    {
        using var outsiderClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(outsiderClient);

        var ownerProject = await CreateProjectAsync(Client, "Owner A Project");
        var outsiderProject = await CreateProjectAsync(outsiderClient, "Owner B Project");

        var ownerResponse = await Client.GetAsync("/api/projects");
        var outsiderResponse = await outsiderClient.GetAsync("/api/projects");

        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(outsiderResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ownerPayload = await ownerResponse.Content.ReadFromJsonAsync<ProjectListResponse>();
        var outsiderPayload = await outsiderResponse.Content.ReadFromJsonAsync<ProjectListResponse>();

        Assert.That(ownerPayload, Is.Not.Null);
        Assert.That(outsiderPayload, Is.Not.Null);
        Assert.That(ownerPayload!.Items.Select(item => item.Id), Is.EqualTo(new[] { ownerProject.Id }));
        Assert.That(outsiderPayload!.Items.Select(item => item.Id), Is.EqualTo(new[] { outsiderProject.Id }));
        Assert.That(ownerPayload.Items.Single().Role, Is.EqualTo("專案擁有者"));
        Assert.That(outsiderPayload.Items.Single().Role, Is.EqualTo("專案擁有者"));
    }

    [Test]
    public async Task CreateProject_WithBlankName_ReturnsValidationError()
    {
        var response = await Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("   "));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(response);

        Assert.That(errors, Does.ContainKey("name"));
        Assert.That(errors["name"], Does.Contain("專案名稱為必填欄位"));
    }

    [Test]
    public async Task CreateProject_WhenAnonymousUser_ReturnsUnauthorized()
    {
        using var anonymousClient = CreateAnonymousClient();

        var response = await anonymousClient.PostAsJsonAsync("/api/projects", new CreateProjectRequest("RonFlow Project"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateProject_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-project-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-project-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);
        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Stale Session Project"));

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task CreateProject_WithValidName_AppliesDefaultWorkflowAndCreatesEmptyBoard()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("RonFlow Project"));

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var project = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.That(project, Is.Not.Null);
        Assert.That(project!.Name, Is.EqualTo("RonFlow Project"));
        Assert.That(project.WorkflowStates.Select(state => state.Key), Is.EqualTo(new[] { "todo", "active", "review", "done" }));
        Assert.That(project.WorkflowStates.Select(state => state.Label), Is.EqualTo(new[] { "待處理", "進行中", "審查中", "已完成" }));
        Assert.That(project.WorkflowStates.Single(state => state.IsInitialState).Key, Is.EqualTo("todo"));
        Assert.That(project.WorkflowStates.Single(state => state.IsCompletedState).Key, Is.EqualTo("done"));

        var boardResponse = await Client.GetAsync($"/api/projects/{project.Id}/board");

        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();

        Assert.That(board, Is.Not.Null);
        Assert.That(board!.ProjectId, Is.EqualTo(project.Id));
        Assert.That(board.ProjectName, Is.EqualTo("RonFlow Project"));
        Assert.That(board.Columns.Select(column => column.StateKey), Is.EqualTo(new[] { "todo", "active", "review", "done" }));
        Assert.That(board.Columns.Select(column => column.Label), Is.EqualTo(new[] { "待處理", "進行中", "審查中", "已完成" }));
        Assert.That(board.Columns.Single(column => column.IsCompletedState).StateKey, Is.EqualTo("done"));
        Assert.That(board.Columns.All(column => column.Tasks.Count == 0), Is.True);
        Assert.That(board.Columns.All(column => column.EmptyStateMessage == "目前沒有任務"), Is.True);
    }

    [Test]
    public async Task GetBoard_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var response = await Client.GetAsync($"/api/projects/{Guid.NewGuid()}/board");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetBoard_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-board-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-board-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");

        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.GetAsync($"/api/projects/{project.Id}/board");

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task GetBoard_WhenProjectBelongsToAnotherUser_ReturnsAccessDenied()
    {
        using var outsiderClient = CreateAuthenticatedClient(TestUser.OwnerB);
        var project = await CreateProjectAsync(Client, "Owner A Project");

        var response = await outsiderClient.GetAsync($"/api/projects/{project.Id}/board");

        await AssertAccessDeniedAsync(response);
    }

    [Test]
    public async Task GetCodeTraceability_WhenProjectHasTraceableTasks_ReturnsFlattenedTraceabilityItems()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var traceableTask = await CreateTaskAsync(project.Id, "Traceable Task");
        await CreateTaskAsync(project.Id, "Empty Task");

        var acquireResponse = await Client.PostAsync($"/api/projects/{project.Id}/tasks/{traceableTask.Id}/content-edit-lock", content: null);
        Assert.That(acquireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = """
                      {
                          "title": "Traceable Task",
                          "description": "Records code changes",
                          "dueDate": null,
                          "codeTraceability": {
                              "api": [
                                  { "changeType": "added", "target": "GET /api/projects/{projectId}/code-traceability" }
                              ],
                              "frontendPages": [
                                  { "changeType": "modified", "target": "Project Board" }
                              ],
                              "frontendComponents": [
                                  { "changeType": "added", "target": "CodeTraceabilityQueryView" }
                              ]
                          }
                      }
                      """;

        var updateResponse = await Client.PatchAsync(
            $"/api/projects/{project.Id}/tasks/{traceableTask.Id}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var response = await Client.GetAsync($"/api/projects/{project.Id}/code-traceability");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ProjectCodeTraceabilityResponse>();

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Select(item => item.TaskTitle).Distinct().ToArray(), Is.EqualTo(new[] { "Traceable Task" }));
        Assert.That(result.Items.Select(item => item.Category).ToArray(), Is.EqualTo(new[] { "api", "frontendPages", "frontendComponents" }));
        Assert.That(result.Items.Select(item => item.ChangeType).ToArray(), Is.EqualTo(new[] { "added", "modified", "added" }));
        Assert.That(result.Items.Select(item => item.Target).ToArray(), Is.EqualTo(new[]
        {
            "GET /api/projects/{projectId}/code-traceability",
            "Project Board",
            "CodeTraceabilityQueryView",
        }));
    }

    [Test]
    public async Task ReplaceProjectSubtaskTemplates_WithValidPayload_PersistsOrderedTemplates()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var replaceResponse = await Client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/subtask-templates",
            new
            {
                items = new[]
                {
                    new { title = "需求已釐清" },
                    new { title = "spec 文件已寫入" },
                    new { title = "已部署到 localhost" },
                },
            });

        Assert.That(replaceResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var replacedTemplates = await replaceResponse.Content.ReadFromJsonAsync<ProjectSubtaskTemplateListResponse>();

        Assert.That(replacedTemplates, Is.Not.Null);
        Assert.That(replacedTemplates!.Items.Select(item => item.Title), Is.EqualTo(new[]
        {
            "需求已釐清",
            "spec 文件已寫入",
            "已部署到 localhost",
        }));
        Assert.That(replacedTemplates.Items.Select(item => item.Order), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(replacedTemplates.Items.All(item => item.Id != Guid.Empty), Is.True);

        var getResponse = await Client.GetAsync($"/api/projects/{project.Id}/subtask-templates");

        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var fetchedTemplates = await getResponse.Content.ReadFromJsonAsync<ProjectSubtaskTemplateListResponse>();

        Assert.That(fetchedTemplates, Is.Not.Null);
        Assert.That(fetchedTemplates!.Items.Select(item => item.Title), Is.EqualTo(new[]
        {
            "需求已釐清",
            "spec 文件已寫入",
            "已部署到 localhost",
        }));
    }
}