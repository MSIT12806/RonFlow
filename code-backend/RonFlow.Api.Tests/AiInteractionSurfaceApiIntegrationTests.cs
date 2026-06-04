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
        Assert.That(payload, Does.Contain("canonical_base_paths:"));
        Assert.That(payload, Does.Contain("ronflow_api_base_url: http://localhost/ronflow-api/api"));
        Assert.That(payload, Does.Contain("ronauth_api_base_url: http://localhost/ronauth-api/api/auth"));
        Assert.That(payload, Does.Contain("serves the UI shell"));
        Assert.That(payload, Does.Contain("canonical_entrypoints:"));
        Assert.That(payload, Does.Contain("- GET /api/ai/projects/{projectId}/board-summary"));
        Assert.That(payload, Does.Contain("- GET /api/ai/projects/{projectId}/current-work-summary"));
        Assert.That(payload, Does.Contain("- GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary"));
        Assert.That(payload, Does.Contain("- POST /api/ai/active-scope"));
        Assert.That(payload, Does.Contain("- POST /api/ai/apply"));
        Assert.That(payload, Does.Contain("login_contract:"));
        Assert.That(payload, Does.Contain("request_body: {\"userName\":\"<user-name>\",\"password\":\"<password>\"}"));
        Assert.That(payload, Does.Contain("login reads `userName`, not `email`"));
        Assert.That(payload, Does.Contain("1. 讀取 capabilities manifest"));
        Assert.That(payload, Does.Contain("3. 讀取 workflow guidance"));
        Assert.That(payload, Does.Contain("5. 讀取 project list summary"));
        Assert.That(payload, Does.Contain("6. 視需要讀取 invitation inbox summary"));
        Assert.That(payload, Does.Contain("後續細節以系統回傳的 contract 為準"));
    }

    [Test]
    public async Task GetGlossary_WhenAuthenticated_ReturnsCanonicalGlossaryText()
    {
        var response = await Client.GetAsync("/api/ai/glossary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow AI Glossary v1"));
        Assert.That(payload, Does.Contain("term: bootstrap"));
        Assert.That(payload, Does.Contain("term: active scope"));
        Assert.That(payload, Does.Contain("term: workflow guidance"));
    }

    [Test]
    public async Task GetCapabilities_WhenAuthenticated_ReturnsCanonicalManifestText()
    {
        var response = await Client.GetAsync("/api/ai/capabilities");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Capabilities Manifest v1"));
        Assert.That(payload, Does.Contain("scope_activation_contract:"));
        Assert.That(payload, Does.Contain("endpoint: POST /api/ai/active-scope"));
        Assert.That(payload, Does.Contain("body_shape: {\"projectId\":\"<project-id>\"}"));
        Assert.That(payload, Does.Contain("success_status: 204 No Content"));
        Assert.That(payload, Does.Contain("apply_endpoint: POST /api/ai/apply"));
        Assert.That(payload, Does.Contain("required_input_location: requiredFields.<inputName>"));
        Assert.That(payload, Does.Contain("- capability: read_session_summary"));
        Assert.That(payload, Does.Contain("read_endpoint: GET /api/ai/session-summary"));
        Assert.That(payload, Does.Contain("- capability: read_current_work_summary"));
        Assert.That(payload, Does.Contain("read_endpoint: GET /api/ai/projects/{projectId}/current-work-summary"));
        Assert.That(payload, Does.Contain("route_params: projectId"));
        Assert.That(payload, Does.Contain("- projectId <- read_project_list_summary.project_id or read_session_summary.active_scope"));
        Assert.That(payload, Does.Contain("read_endpoint: GET /api/ai/projects/{projectId}/tasks/{taskId}/detail-summary"));
        Assert.That(payload, Does.Contain("- taskId <- read_project_board_summary.visible_tasks.task_id or read_current_work_summary.open_tasks.task_id"));
        Assert.That(payload, Does.Contain("- capability: read_audit_entry"));
        Assert.That(payload, Does.Contain("read_endpoint: GET /api/ai/audit-entries/{auditEntryId}"));
        Assert.That(payload, Does.Contain("- capability: read_invitation_inbox_summary"));
        Assert.That(payload, Does.Contain("read_endpoint: GET /api/ai/invitations/summary"));
        Assert.That(payload, Does.Contain("- capability: create_task"));
        Assert.That(payload, Does.Contain("required_fields_path: requiredFields.projectId, requiredFields.title"));
        Assert.That(payload, Does.Contain("\"operation\":\"create_task\""));
        Assert.That(payload, Does.Contain("- capability: invite_project_member"));
        Assert.That(payload, Does.Contain("- capability: accept_project_invitation"));
        Assert.That(payload, Does.Contain("required_fields_path: requiredFields.invitationId"));
        Assert.That(payload, Does.Contain("\"operation\":\"accept_project_invitation\""));
        Assert.That(payload, Does.Contain("\"requiredFields\":{\"invitationId\":\"<invitation-id>\"}"));
        Assert.That(payload, Does.Contain("- capability: reject_project_invitation"));
        Assert.That(payload, Does.Contain("- capability: check_task_subtask"));
        Assert.That(payload, Does.Contain("- capability: uncheck_task_subtask"));
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
        Assert.That(payload, Does.Contain("canonical_discovery_path:"));
        Assert.That(payload, Does.Contain("- discover projects -> GET /api/ai/projects/summary -> yields projectId"));
        Assert.That(payload, Does.Contain("- inspect scoped work -> GET /api/ai/projects/{projectId}/board-summary or GET /api/ai/projects/{projectId}/current-work-summary -> yields taskId"));
        Assert.That(payload, Does.Contain("task_start_rules:"));
        Assert.That(payload, Does.Contain("- when the human asks the AI to execute a RonFlow task and the confirmed task is in Todo, move it to Active before implementation work begins"));
        Assert.That(payload, Does.Contain("- use move_task_state with targetStateKey: Active"));
        Assert.That(payload, Does.Contain("checklist_rules:"));
        Assert.That(payload, Does.Contain("- use check_task_subtask when one checklist item is finished"));
        Assert.That(payload, Does.Contain("invitation_rules:"));
        Assert.That(payload, Does.Contain("read_invitation_inbox_summary"));
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
    public async Task GetInvitationInboxSummary_WhenPendingInvitationExists_ReturnsCanonicalSummaryText()
    {
        await EnsureKnownUserAsync(Client);
        using var knownInviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);
        await EnsureKnownUserAsync(knownInviteeClient);
        var project = await CreateProjectAsync("AI Invitation Inbox Project");
        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var inviteeClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-ai-invitation-inbox");
        await ActivateSessionAsync(inviteeClient);

        var response = await inviteeClient.GetAsync("/api/ai/invitations/summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Invitation Inbox Summary v1"));
        Assert.That(payload, Does.Contain("pending_invitation_count: 1"));
        Assert.That(payload, Does.Contain($"project_id: {project.Id}"));
        Assert.That(payload, Does.Contain("project_name: AI Invitation Inbox Project"));
        Assert.That(payload, Does.Contain("inviter_name: owner-a"));
        Assert.That(payload, Does.Contain("next_actions:"));
        Assert.That(payload, Does.Contain("- accept_project_invitation"));
        Assert.That(payload, Does.Contain("- reject_project_invitation"));
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
    public async Task GetCurrentWorkSummary_WhenProjectExists_ReturnsOpenTasksText()
    {
        await EnsureKnownUserAsync(Client);
        var project = await CreateProjectAsync("AI Current Work Project");
        var task = await CreateTaskAsync(project.Id, "Build AI Discovery Surface");

        var response = await Client.GetAsync($"/api/ai/projects/{project.Id}/current-work-summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Current Work Summary v1"));
        Assert.That(payload, Does.Contain($"project_id: {project.Id}"));
        Assert.That(payload, Does.Contain("open_task_count: 1"));
        Assert.That(payload, Does.Contain("open_tasks:"));
        Assert.That(payload, Does.Contain($"task_id: {task.Id}"));
        Assert.That(payload, Does.Contain("title: Build AI Discovery Surface"));
        Assert.That(payload, Does.Contain("workflow_state_key: Todo"));
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
    public async Task GetTaskDetailSummary_WhenTaskHasChecklist_IncludesSubtasksAndAiFriendlyNextActions()
    {
        await EnsureKnownUserAsync(Client);
        var project = await CreateProjectAsync("AI Checklist Summary Project");

        var templateResponse = await Client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/subtask-templates",
            new
            {
                items = new[]
                {
                    new { title = "需求已釐清" },
                    new { title = "驗收案例已確認" },
                },
            });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var task = await CreateTaskAsync(project.Id, "Build AI Checklist Summary");

        var response = await Client.GetAsync($"/api/ai/projects/{project.Id}/tasks/{task.Id}/detail-summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("subtasks:"));
        Assert.That(payload, Does.Contain("title: 需求已釐清"));
        Assert.That(payload, Does.Contain("is_checked: no"));
        Assert.That(payload, Does.Contain("- check_task_subtask"));
        Assert.That(payload, Does.Contain("- uncheck_task_subtask"));
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
    public async Task ApplyInviteProjectMember_WhenScopeIsActive_CreatesPendingInvitationWithAuditEntry()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-apply-invite-member");

        await EnsureKnownUserAsync(sessionClient);
        using var knownInviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);
        await EnsureKnownUserAsync(knownInviteeClient);
        await ActivateSessionAsync(sessionClient);

        var project = await CreateProjectAsync(sessionClient, "AI Apply Invite Project");
        var activateResponse = await sessionClient.PostAsJsonAsync("/api/ai/active-scope", new { projectId = project.Id });
        Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var response = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "invite_project_member",
            targetType = "project",
            targetId = project.Id,
            requiredFields = new
            {
                projectId = project.Id,
                invitee = TestUser.OwnerB.Email,
            },
            optionalFields = new { },
            note = "invite member from ai apply",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Apply Result v1"));
        Assert.That(payload, Does.Contain("operation: invite_project_member"));
        Assert.That(payload, Does.Contain($"target_id: {project.Id}"));
        Assert.That(payload, Does.Contain("- invitations"));
        Assert.That(payload, Does.Contain("audit_entry_id:"));

        var invitationsResponse = await sessionClient.GetAsync($"/api/projects/{project.Id}/invitations");
        Assert.That(invitationsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var invitations = await invitationsResponse.Content.ReadFromJsonAsync<ProjectInvitationListResponse>();
        Assert.That(invitations, Is.Not.Null);
        Assert.That(invitations!.Items.Single().Invitee, Is.EqualTo(TestUser.OwnerB.Email));
        Assert.That(invitations.Items.Single().Status, Is.EqualTo("Pending"));
    }

    [Test]
    public async Task ApplyAcceptProjectInvitation_WhenInvitationIsPending_AddsProjectScopeWithAuditEntry()
    {
        await EnsureKnownUserAsync(Client);
        using var knownInviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);
        await EnsureKnownUserAsync(knownInviteeClient);
        var project = await CreateProjectAsync("AI Accept Invitation Project");
        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        using var inviteeClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-ai-apply-accept-invitation");
        await ActivateSessionAsync(inviteeClient);

        var response = await inviteeClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "accept_project_invitation",
            targetType = "invitation",
            targetId = invitation!.Id,
            requiredFields = new
            {
                invitationId = invitation.Id,
            },
            optionalFields = new { },
            note = "accept invitation from ai apply",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Apply Result v1"));
        Assert.That(payload, Does.Contain("operation: accept_project_invitation"));
        Assert.That(payload, Does.Contain($"target_id: {invitation.Id}"));
        Assert.That(payload, Does.Contain("- membership"));
        Assert.That(payload, Does.Contain("audit_entry_id:"));

        var projectsSummaryResponse = await inviteeClient.GetAsync("/api/ai/projects/summary");
        Assert.That(projectsSummaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var projectsSummary = await projectsSummaryResponse.Content.ReadAsStringAsync();
        Assert.That(projectsSummary, Does.Contain($"project_id: {project.Id}"));
        Assert.That(projectsSummary, Does.Contain("project_name: AI Accept Invitation Project"));
    }

    [Test]
    public async Task ApplyRejectProjectInvitation_WhenInvitationIsPending_RemovesItFromInbox()
    {
        await EnsureKnownUserAsync(Client);
        using var knownInviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);
        await EnsureKnownUserAsync(knownInviteeClient);
        var project = await CreateProjectAsync("AI Reject Invitation Project");
        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        using var inviteeClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-ai-apply-reject-invitation");
        await ActivateSessionAsync(inviteeClient);

        var response = await inviteeClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "reject_project_invitation",
            targetType = "invitation",
            targetId = invitation!.Id,
            requiredFields = new
            {
                invitationId = invitation.Id,
            },
            optionalFields = new { },
            note = "reject invitation from ai apply",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("operation: reject_project_invitation"));
        Assert.That(payload, Does.Contain("- invitation_status"));

        var inboxResponse = await inviteeClient.GetAsync("/api/ai/invitations/summary");
        Assert.That(inboxResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var inboxPayload = await inboxResponse.Content.ReadAsStringAsync();
        Assert.That(inboxPayload, Does.Contain("pending_invitation_count: 0"));
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
        Assert.That(payload, Does.Contain("message: Required apply field `requiredFields.title` is missing."));
        Assert.That(payload, Does.Contain("POST /api/ai/apply reads `title` from the `requiredFields` object, not from the top-level body."));
        Assert.That(payload, Does.Contain("recovery_hint: Provide `requiredFields.title` and submit the apply request again."));
    }

    [Test]
    public async Task ApplyAcceptProjectInvitation_WhenInvitationIdIsTopLevel_ReturnsRequiredFieldsPathHint()
    {
        await EnsureKnownUserAsync(Client);
        using var knownInviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);
        await EnsureKnownUserAsync(knownInviteeClient);
        var project = await CreateProjectAsync("AI Accept Top Level Invitation Project");
        var invitationResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(invitationResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await invitationResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();
        Assert.That(invitation, Is.Not.Null);

        using var inviteeClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-ai-apply-accept-top-level-invitation");
        await ActivateSessionAsync(inviteeClient);

        var response = await inviteeClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "accept_project_invitation",
            targetType = "invitation",
            targetId = invitation!.Id,
            invitationId = invitation.Id,
            requiredFields = new { },
            optionalFields = new { },
            note = "invitation id intentionally placed at top level",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var payload = await response.Content.ReadAsStringAsync();

        Assert.That(payload, Does.Contain("RonFlow Error v1"));
        Assert.That(payload, Does.Contain("error_code: ValidationFailed"));
        Assert.That(payload, Does.Contain("message: Required apply field `requiredFields.invitationId` is missing."));
        Assert.That(payload, Does.Contain("POST /api/ai/apply reads `invitationId` from the `requiredFields` object, not from the top-level body."));
        Assert.That(payload, Does.Contain("recovery_hint: Provide `requiredFields.invitationId` and submit the apply request again."));
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
    public async Task ApplyCheckAndUncheckTaskSubtask_WhenScopeIsActive_TogglesChecklistItem()
    {
        using var sessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-ai-apply-toggle-subtask");

        await EnsureKnownUserAsync(sessionClient);
        await ActivateSessionAsync(sessionClient);

        var project = await CreateProjectAsync(sessionClient, "AI Toggle Checklist Project");
        var templateResponse = await sessionClient.PutAsJsonAsync(
            $"/api/projects/{project.Id}/subtask-templates",
            new
            {
                items = new[]
                {
                    new { title = "完成 discovery" },
                    new { title = "更新 contract" },
                },
            });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var task = await CreateTaskAsync(sessionClient, project.Id, "Implement AI checklist contract");

        var activateResponse = await sessionClient.PostAsJsonAsync("/api/ai/active-scope", new { projectId = project.Id });
        Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var targetSubtaskId = task.Subtasks.First().Id;

        var checkResponse = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "check_task_subtask",
            targetType = "task",
            targetId = task.Id,
            requiredFields = new
            {
                taskId = task.Id,
                subtaskId = targetSubtaskId,
            },
            optionalFields = new { },
            note = "mark one checklist item complete",
        });

        Assert.That(checkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var checkPayload = await checkResponse.Content.ReadAsStringAsync();

        Assert.That(checkPayload, Does.Contain("operation: check_task_subtask"));
        Assert.That(checkPayload, Does.Contain("- subtasks"));

        var checkedDetailResponse = await sessionClient.GetAsync($"/api/projects/{project.Id}/tasks/{task.Id}");
        Assert.That(checkedDetailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var checkedDetail = await checkedDetailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();
        Assert.That(checkedDetail, Is.Not.Null);
        Assert.That(checkedDetail!.Subtasks.Single(item => item.Id == targetSubtaskId).IsChecked, Is.True);

        var uncheckResponse = await sessionClient.PostAsJsonAsync("/api/ai/apply", new
        {
            operation = "uncheck_task_subtask",
            targetType = "task",
            targetId = task.Id,
            requiredFields = new
            {
                taskId = task.Id,
                subtaskId = targetSubtaskId,
            },
            optionalFields = new { },
            note = "reopen one checklist item",
        });

        Assert.That(uncheckResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var uncheckPayload = await uncheckResponse.Content.ReadAsStringAsync();

        Assert.That(uncheckPayload, Does.Contain("operation: uncheck_task_subtask"));
        Assert.That(uncheckPayload, Does.Contain("- subtasks"));

        var uncheckedDetailResponse = await sessionClient.GetAsync($"/api/projects/{project.Id}/tasks/{task.Id}");
        Assert.That(uncheckedDetailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var uncheckedDetail = await uncheckedDetailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();
        Assert.That(uncheckedDetail, Is.Not.Null);
        Assert.That(uncheckedDetail!.Subtasks.Single(item => item.Id == targetSubtaskId).IsChecked, Is.False);
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
