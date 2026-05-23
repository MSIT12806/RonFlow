using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class ProjectApiIntegrationTests : ApiIntegrationTestBase
{
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
    public async Task GetBoard_WhenProjectBelongsToAnotherUser_ReturnsAccessDenied()
    {
        using var outsiderClient = CreateAuthenticatedClient(TestUser.OwnerB);
        var project = await CreateProjectAsync(Client, "Owner A Project");

        var response = await outsiderClient.GetAsync($"/api/projects/{project.Id}/board");

        await AssertAccessDeniedAsync(response);
    }
}