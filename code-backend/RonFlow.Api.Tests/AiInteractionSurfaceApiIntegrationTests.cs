using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;

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
        var task = await CreateTaskAsync(project.Id, "Build AI Board");

        var response = await Client.GetAsync($"/api/ai/projects/{project.Id}/board-summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Project Board Summary v1"));
        Assert.That(payload, Does.Contain($"project_id: {project.Id}"));
        Assert.That(payload, Does.Contain("workflow_columns:"));
        Assert.That(payload, Does.Contain("- key: Todo"));
        Assert.That(payload, Does.Contain("- key: Active"));
        Assert.That(payload, Does.Contain("visible_tasks:"));
        Assert.That(payload, Does.Contain($"task_id: {task.Id}"));
        Assert.That(payload, Does.Contain("title: Build AI Board"));
        Assert.That(payload, Does.Contain("workflow_state_key: Todo"));
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

    [Test]
    public async Task ActivateScope_WhenProjectExists_UpdatesSessionSummaryActiveScope()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-scope-session");

        await EnsureKnownUserAsync(sessionClient);
        await ActivateSessionAsync(sessionClient);

        var project = await CreateProjectAsync(sessionClient, "AI Scope Project");
        var activateResponse = await sessionClient.PostAsJsonAsync("/api/ai/active-scope", new { projectId = project.Id });

        Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var sessionSummaryResponse = await sessionClient.GetAsync("/api/ai/session-summary");
        Assert.That(sessionSummaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await sessionSummaryResponse.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Session Summary v1"));
        Assert.That(payload, Does.Contain($"active_scope: {project.Id}"));
    }

    [Test]
    public async Task ApplyCreateProject_WhenRequestIsValid_ReturnsApplyResultWithAuditEntryId()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-apply-create-project");

        await EnsureKnownUserAsync(sessionClient);
        await ActivateSessionAsync(sessionClient);

        var response = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "create_project",
            targetType = "project",
            targetId = "new",
            requiredFields = new
            {
                name = "AI Apply Project",
            },
            optionalFields = new { },
            note = "integration create project",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Apply Result v1"));
        Assert.That(payload, Does.Contain("status: success"));
        Assert.That(payload, Does.Contain("operation: create_project"));
        Assert.That(payload, Does.Contain("target_type: project"));
        Assert.That(payload, Does.Contain("changed_fields:"));
        Assert.That(payload, Does.Contain("audit_entry_id:"));
    }

    [Test]
    public async Task ApplyCreateTask_WhenRequiredFieldIsMissing_ReturnsValidationFailedErrorContract()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-apply-create-task-error");

        await EnsureKnownUserAsync(sessionClient);
        await ActivateSessionAsync(sessionClient);

        var project = await CreateProjectAsync(sessionClient, "AI Apply Task Project");
        await sessionClient.PostAsJsonAsync("/api/ai/active-scope", new { projectId = project.Id });

        var response = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "create_task",
            targetType = "task",
            targetId = "new",
            requiredFields = new
            {
                projectId = project.Id,
            },
            optionalFields = new { },
            note = "missing title on purpose",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Error v1"));
        Assert.That(payload, Does.Contain("error_code: ValidationFailed"));
        Assert.That(payload, Does.Contain("message: Required field `title` is missing."));
        Assert.That(payload, Does.Contain("recovery_hint: Provide `title` and submit the write request again."));
    }

    [Test]
    public async Task ApplyUpdateTaskDetail_WhenScopeIsMissing_ReturnsScopeRequiredErrorContract()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-apply-scope-missing");

        await EnsureKnownUserAsync(sessionClient);
        await ActivateSessionAsync(sessionClient);

        var project = await CreateProjectAsync(sessionClient, "AI Scope Missing Project");
        var task = await CreateTaskAsync(sessionClient, project.Id, "AI Scope Missing Task");

        var response = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "update_task_detail",
            targetType = "task",
            targetId = task.Id,
            requiredFields = new
            {
                taskId = task.Id,
            },
            optionalFields = new
            {
                title = "Updated Without Scope",
            },
            note = "scope missing",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Error v1"));
        Assert.That(payload, Does.Contain("error_code: ScopeRequired"));
        Assert.That(payload, Does.Contain("recovery_hint:"));
    }

    [Test]
    public async Task ApplyUpdateTaskDetail_WhenScopeIsActive_ReturnsApplyResultAndAuditEntryCanBeReadBack()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-apply-update-task");

        await EnsureKnownUserAsync(sessionClient);
        await ActivateSessionAsync(sessionClient);

        var project = await CreateProjectAsync(sessionClient, "AI Update Project");
        var task = await CreateTaskAsync(sessionClient, project.Id, "Original AI Task");

        var activateResponse = await sessionClient.PostAsJsonAsync("/api/ai/active-scope", new { projectId = project.Id });
        Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var response = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "update_task_detail",
            targetType = "task",
            targetId = task.Id,
            requiredFields = new
            {
                taskId = task.Id,
            },
            optionalFields = new
            {
                title = "Updated AI Task",
                dueDate = "2026-06-01",
            },
            note = "update from ai apply",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Apply Result v1"));
        Assert.That(payload, Does.Contain("operation: update_task_detail"));
        Assert.That(payload, Does.Contain($"target_id: {task.Id}"));
        Assert.That(payload, Does.Contain("- title"));
        Assert.That(payload, Does.Contain("- dueDate"));

        var auditEntryId = payload
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Single(line => line.StartsWith("audit_entry_id:", StringComparison.Ordinal))
            .Substring("audit_entry_id:".Length)
            .Trim();

        var auditResponse = await sessionClient.GetAsync($"/api/ai/audit-entries/{auditEntryId}");

        Assert.That(auditResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var auditPayload = await auditResponse.Content.ReadAsStringAsync();

        Assert.That(auditPayload, Does.Contain("RonFlow Audit Entry v1"));
        Assert.That(auditPayload, Does.Contain($"audit_entry_id: {auditEntryId}"));
        Assert.That(auditPayload, Does.Contain("actor_type: ai"));
        Assert.That(auditPayload, Does.Contain("requested_change: update_task_detail"));
        Assert.That(auditPayload, Does.Contain($"target_id: {task.Id}"));
        Assert.That(auditPayload, Does.Contain("actual_diff:"));
    }

    [Test]
    public async Task ApplyReorderTask_WhenTargetIndexIsValid_ReordersTasksWithinWorkflowColumn()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-apply-reorder-task");

        await EnsureKnownUserAsync(sessionClient);
        await ActivateSessionAsync(sessionClient);

        var project = await CreateProjectAsync(sessionClient, "AI Reorder Project");
        var firstTask = await CreateTaskAsync(sessionClient, project.Id, "Task A");
        var secondTask = await CreateTaskAsync(sessionClient, project.Id, "Task B");

        var activateResponse = await sessionClient.PostAsJsonAsync("/api/ai/active-scope", new { projectId = project.Id });
        Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var reorderResponse = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "reorder_task",
            targetType = "task",
            targetId = secondTask.Id,
            requiredFields = new
            {
                taskId = secondTask.Id,
                targetStateKey = "Todo",
                targetIndex = 0,
            },
            optionalFields = new { },
            note = "move task B before task A",
        });

        Assert.That(reorderResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await reorderResponse.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Apply Result v1"));
        Assert.That(payload, Does.Contain("operation: reorder_task"));
        Assert.That(payload, Does.Contain($"target_id: {secondTask.Id}"));
        Assert.That(payload, Does.Contain("- sort_order"));

        var boardResponse = await sessionClient.GetAsync($"/api/projects/{project.Id}/board");
        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var board = await boardResponse.Content.ReadFromJsonAsync<ProjectBoardResponse>();
        Assert.That(board, Is.Not.Null);

        var todoTaskIds = board!.Columns
            .Single(column => column.StateKey == "todo")
            .Tasks
            .Select(task => task.Id)
            .ToArray();

        Assert.That(todoTaskIds, Is.EqualTo(new[] { secondTask.Id, firstTask.Id }));
    }
}