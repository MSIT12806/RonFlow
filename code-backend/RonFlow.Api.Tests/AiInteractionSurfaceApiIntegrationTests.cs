using System.Net;

namespace RonFlow.Api.Tests;

public sealed class AiInteractionSurfaceApiIntegrationTests : ApiIntegrationTestBase
{
    [Test]
    public async Task GetBootstrap_WhenAuthenticated_ReturnsCanonicalBootstrapText()
    {
        var response = await Client.GetAsync("/api/ai/bootstrap");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Bootstrap v1"));
        Assert.That(payload, Does.Contain("RonFlow 是一個專案管理工具。"));
        Assert.That(payload, Does.Contain("1. 讀取 capabilities manifest"));
        Assert.That(payload, Does.Contain("4. 讀取 project list summary"));
    }

    [Test]
    public async Task GetCapabilities_WhenAuthenticated_ReturnsCanonicalManifestText()
    {
        var response = await Client.GetAsync("/api/ai/capabilities");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Capabilities Manifest v1"));
        Assert.That(payload, Does.Contain("- capability: create_task"));
        Assert.That(payload, Does.Contain("active_scope_required: yes"));
        Assert.That(payload, Does.Contain("required_inputs: projectId, title"));
    }

    [Test]
    public async Task GetWorkflowGuidance_WhenAuthenticated_ReturnsCanonicalGuidanceText()
    {
        var response = await Client.GetAsync("/api/ai/workflow-guidance");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Workflow Guidance v1"));
        Assert.That(payload, Does.Contain("1. read summary"));
        Assert.That(payload, Does.Contain("4. prepare write request"));
        Assert.That(payload, Does.Contain("6. inspect result"));
    }

    [Test]
    public async Task GetSessionSummary_WhenNoActiveScope_ReturnsActiveSessionAndNoScope()
    {
        var response = await Client.GetAsync("/api/ai/session-summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Session Summary v1"));
        Assert.That(payload, Does.Contain("session_status: active"));
        Assert.That(payload, Does.Contain("actor_type: ai"));
        Assert.That(payload, Does.Contain("active_scope: none"));
        Assert.That(payload, Does.Contain("available_scopes:"));
    }

    [Test]
    public async Task GetProjectListSummary_WhenProjectExists_ReturnsProjectAndNextActions()
    {
        await EnsureKnownUserAsync(Client);
        var project = await CreateProjectAsync("AI Project Summary");

        var response = await Client.GetAsync("/api/ai/projects/summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Project List Summary v1"));
        Assert.That(payload, Does.Contain("projects_count: 1"));
        Assert.That(payload, Does.Contain($"project_id: {project.Id}"));
        Assert.That(payload, Does.Contain("project_name: AI Project Summary"));
        Assert.That(payload, Does.Contain("next_actions:"));
        Assert.That(payload, Does.Contain("- read_project_board_summary"));
    }

    [Test]
    public async Task GetProjectBoardSummary_WhenProjectExists_ReturnsCanonicalBoardText()
    {
        await EnsureKnownUserAsync(Client);
        var project = await CreateProjectAsync("AI Board Project");
        await CreateTaskAsync(project.Id, "Build AI Board");

        var response = await Client.GetAsync($"/api/ai/projects/{project.Id}/board-summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Project Board Summary v1"));
        Assert.That(payload, Does.Contain($"project_id: {project.Id}"));
        Assert.That(payload, Does.Contain("workflow_columns:"));
        Assert.That(payload, Does.Contain("- key: Todo"));
        Assert.That(payload, Does.Contain("- key: Active"));
        Assert.That(payload, Does.Contain("next_actions:"));
    }

    [Test]
    public async Task GetTaskDetailSummary_WhenTaskExists_ReturnsCanonicalTaskSummaryText()
    {
        await EnsureKnownUserAsync(Client);
        var project = await CreateProjectAsync("AI Task Project");
        var task = await CreateTaskAsync(project.Id, "Build AI Task Summary");

        var response = await Client.GetAsync($"/api/ai/projects/{project.Id}/tasks/{task.Id}/detail-summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Task Detail Summary v1"));
        Assert.That(payload, Does.Contain($"task_id: {task.Id}"));
        Assert.That(payload, Does.Contain("title: Build AI Task Summary"));
        Assert.That(payload, Does.Contain("workflow_state_key: Todo"));
        Assert.That(payload, Does.Contain("next_actions:"));
        Assert.That(payload, Does.Contain("- update_task_detail"));
    }

    [Test]
    public async Task GetProjectBoardSummary_WhenProjectBelongsToAnotherUser_ReturnsForbiddenErrorContract()
    {
        await EnsureKnownUserAsync(Client);
        using var outsiderClient = CreateAuthenticatedClient(TestUser.OwnerB);
        await EnsureKnownUserAsync(outsiderClient);
        var project = await CreateProjectAsync("Owner A Private Project");

        var response = await outsiderClient.GetAsync($"/api/ai/projects/{project.Id}/board-summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Error v1"));
        Assert.That(payload, Does.Contain("error_code: Forbidden"));
        Assert.That(payload, Does.Contain("recovery_hint:"));
    }
}