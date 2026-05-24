using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class ProjectInvitationApiIntegrationTests : ApiIntegrationTestBase
{
    private sealed record CreateProjectInvitationRequest(string? Invitee);

    private sealed record ProjectInvitationResponse(Guid Id, string Invitee, string Status);

    private sealed record ProjectInvitationListResponse(IReadOnlyList<ProjectInvitationResponse> Items);

    private sealed record ProjectMemberResponse(string UserName, string Role);

    private sealed record ProjectMemberListResponse(IReadOnlyList<ProjectMemberResponse> Items);

    private sealed record InvitationInboxItemResponse(Guid Id, Guid ProjectId, string ProjectName, string InviterName);

    private sealed record InvitationInboxResponse(IReadOnlyList<InvitationInboxItemResponse> Items);

    private static async Task<JsonDocument> ReadJsonDocumentAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static IReadOnlyList<string> ReadOnlineUserNames(JsonDocument payload)
    {
        Assert.That(
            payload.RootElement.TryGetProperty("onlineUsers", out var onlineUsers),
            Is.True,
            "Expected members response to include onlineUsers.");

        return onlineUsers
            .EnumerateArray()
            .Select(item =>
            {
                Assert.That(
                    item.TryGetProperty("userName", out var userName),
                    Is.True,
                    "Expected each onlineUsers item to include userName.");

                return userName.GetString();
            })
            .OfType<string>()
            .ToArray();
    }

    private async Task<ProjectInvitationResponse> InviteMemberAsync(Guid projectId, string inviteeEmail)
    {
        var inviteResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/invitations",
            new CreateProjectInvitationRequest(inviteeEmail));

        Assert.That(inviteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await inviteResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();

        Assert.That(invitation, Is.Not.Null);

        return invitation!;
    }

    private static async Task<ProjectInvitationResponse> InviteMemberAsync(HttpClient client, Guid projectId, string inviteeEmail)
    {
        var inviteResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/invitations",
            new CreateProjectInvitationRequest(inviteeEmail));

        Assert.That(inviteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var invitation = await inviteResponse.Content.ReadFromJsonAsync<ProjectInvitationResponse>();

        Assert.That(invitation, Is.Not.Null);

        return invitation!;
    }

    [Test]
    public async Task GetInvitationInbox_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-inbox-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-inbox-session-2");

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);

        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.GetAsync("/api/invitations");

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task GetProjectMembers_WhenAcceptedMemberIsInProjectScope_ReturnsOnlineUsers()
    {
        using var ownerSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-members-presence-session");
        using var memberSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-presence-session-1");
        using var memberBootstrapClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(memberBootstrapClient);

        await ActivateSessionAsync(ownerSessionClient);
        await ActivateSessionAsync(memberSessionClient);

        var project = await CreateProjectAsync("RonFlow Project");
        var invitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);
        var acceptResponse = await memberSessionClient.PostAsync($"/api/invitations/{invitation.Id}/accept", content: null);

        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var enterProjectResponse = await memberSessionClient.GetAsync($"/api/projects/{project.Id}/board");

        Assert.That(enterProjectResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var membersResponse = await ownerSessionClient.GetAsync($"/api/projects/{project.Id}/members");

        Assert.That(membersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var payload = await ReadJsonDocumentAsync(membersResponse);
        var onlineUserNames = ReadOnlineUserNames(payload);

        Assert.That(onlineUserNames, Does.Contain(TestUser.OwnerB.UserName));
    }

    [Test]
    public async Task GetProjectMembers_WhenAcceptedMemberOldSessionIsInvalidatedByNewSession_RemovesOldSessionPresence()
    {
        using var ownerSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-members-presence-observer-session");
        using var memberSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-presence-session-1");
        using var replacementSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-presence-session-2");
        using var memberBootstrapClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(memberBootstrapClient);

        await ActivateSessionAsync(ownerSessionClient);
        await ActivateSessionAsync(memberSessionClient);

        var project = await CreateProjectAsync("RonFlow Project");
        var invitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);
        var acceptResponse = await memberSessionClient.PostAsync($"/api/invitations/{invitation.Id}/accept", content: null);

        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var enterProjectResponse = await memberSessionClient.GetAsync($"/api/projects/{project.Id}/board");

        Assert.That(enterProjectResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var beforeInvalidationResponse = await ownerSessionClient.GetAsync($"/api/projects/{project.Id}/members");

        Assert.That(beforeInvalidationResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var beforePayload = await ReadJsonDocumentAsync(beforeInvalidationResponse))
        {
            var onlineUserNames = ReadOnlineUserNames(beforePayload);
            Assert.That(onlineUserNames, Does.Contain(TestUser.OwnerB.UserName));
        }

        await ActivateSessionAsync(replacementSessionClient);

        var afterInvalidationResponse = await ownerSessionClient.GetAsync($"/api/projects/{project.Id}/members");

        Assert.That(afterInvalidationResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var afterPayload = await ReadJsonDocumentAsync(afterInvalidationResponse);
        var onlineUserNamesAfterInvalidation = ReadOnlineUserNames(afterPayload);

        Assert.That(onlineUserNamesAfterInvalidation, Does.Not.Contain(TestUser.OwnerB.UserName));
    }

    [Test]
    public async Task ReleaseProjectScope_WhenAcceptedMemberLeavesProjectScope_RemovesPresenceFromOnlineUsers()
    {
        using var ownerSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-members-presence-observer-session");
        using var memberSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-project-scope-session-1");
        using var memberBootstrapClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(memberBootstrapClient);

        await ActivateSessionAsync(ownerSessionClient);
        await ActivateSessionAsync(memberSessionClient);

        var project = await CreateProjectAsync("RonFlow Project");
        var invitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);
        var acceptResponse = await memberSessionClient.PostAsync($"/api/invitations/{invitation.Id}/accept", content: null);

        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var enterProjectResponse = await memberSessionClient.GetAsync($"/api/projects/{project.Id}/board");

        Assert.That(enterProjectResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var beforeLeavingResponse = await ownerSessionClient.GetAsync($"/api/projects/{project.Id}/members");

        Assert.That(beforeLeavingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var beforePayload = await ReadJsonDocumentAsync(beforeLeavingResponse))
        {
            var onlineUserNames = ReadOnlineUserNames(beforePayload);
            Assert.That(onlineUserNames, Does.Contain(TestUser.OwnerB.UserName));
        }

        await ReleaseProjectScopeAsync(memberSessionClient);

        var afterLeavingResponse = await ownerSessionClient.GetAsync($"/api/projects/{project.Id}/members");

        Assert.That(afterLeavingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var afterPayload = await ReadJsonDocumentAsync(afterLeavingResponse);
        var onlineUserNamesAfterLeaving = ReadOnlineUserNames(afterPayload);

        Assert.That(onlineUserNamesAfterLeaving, Does.Not.Contain(TestUser.OwnerB.UserName));
    }

    [Test]
    public async Task GetProjectMembers_WhenAcceptedMemberOpensTaskDetailWithinProjectScope_KeepsPresenceInOnlineUsers()
    {
        using var ownerSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-members-presence-observer-session");
        using var memberSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-task-detail-project-scope-session-1");
        using var memberBootstrapClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(memberBootstrapClient);

        await ActivateSessionAsync(ownerSessionClient);
        await ActivateSessionAsync(memberSessionClient);

        var project = await CreateProjectAsync("RonFlow Project");
        var createdTask = await CreateTaskAsync(project.Id, "Build Kanban Board");
        var invitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);
        var acceptResponse = await memberSessionClient.PostAsync($"/api/invitations/{invitation.Id}/accept", content: null);

        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var boardResponse = await memberSessionClient.GetAsync($"/api/projects/{project.Id}/board");
        Assert.That(boardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var taskDetailResponse = await memberSessionClient.GetAsync($"/api/projects/{project.Id}/tasks/{createdTask.Id}");
        Assert.That(taskDetailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var membersResponse = await ownerSessionClient.GetAsync($"/api/projects/{project.Id}/members");

        Assert.That(membersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var payload = await ReadJsonDocumentAsync(membersResponse);
        var onlineUserNames = ReadOnlineUserNames(payload);

        Assert.That(onlineUserNames, Does.Contain(TestUser.OwnerB.UserName));
    }

    [Test]
    public async Task GetProjectMembers_WhenAcceptedMemberSwitchesToAnotherProject_MovesPresenceToNewProject()
    {
        using var firstOwnerClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-project-switch-observer-session");
        using var secondOwnerClient = CreateSessionAuthenticatedClient(TestUser.OwnerB, "owner-b-project-switch-observer-session");
        using var memberClient = CreateSessionAuthenticatedClient(
            new TestUser(new Guid("33333333-3333-3333-3333-333333333333"), "member-c", "member-c@example.test"),
            "member-c-project-switch-session");

        await EnsureKnownUserAsync(firstOwnerClient);
        await EnsureKnownUserAsync(secondOwnerClient);
        await EnsureKnownUserAsync(memberClient);

        await ActivateSessionAsync(firstOwnerClient);
        await ActivateSessionAsync(secondOwnerClient);
        await ActivateSessionAsync(memberClient);

        var firstProject = await CreateProjectAsync(firstOwnerClient, "Presence Source Project");
        var secondProject = await CreateProjectAsync(secondOwnerClient, "Presence Target Project");
        var firstInvitation = await InviteMemberAsync(firstOwnerClient, firstProject.Id, "member-c@example.test");
        var secondInvitation = await InviteMemberAsync(secondOwnerClient, secondProject.Id, "member-c@example.test");

        var acceptFirstResponse = await memberClient.PostAsync($"/api/invitations/{firstInvitation.Id}/accept", content: null);
        var acceptSecondResponse = await memberClient.PostAsync($"/api/invitations/{secondInvitation.Id}/accept", content: null);

        Assert.That(acceptFirstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(acceptSecondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var firstBoardResponse = await memberClient.GetAsync($"/api/projects/{firstProject.Id}/board");
        Assert.That(firstBoardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var beforeSwitchFirstMembersResponse = await firstOwnerClient.GetAsync($"/api/projects/{firstProject.Id}/members");
        var beforeSwitchSecondMembersResponse = await secondOwnerClient.GetAsync($"/api/projects/{secondProject.Id}/members");

        Assert.That(beforeSwitchFirstMembersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(beforeSwitchSecondMembersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var firstProjectPayload = await ReadJsonDocumentAsync(beforeSwitchFirstMembersResponse))
        {
            var onlineUserNames = ReadOnlineUserNames(firstProjectPayload);
            Assert.That(onlineUserNames, Does.Contain("member-c"));
        }

        using (var secondProjectPayload = await ReadJsonDocumentAsync(beforeSwitchSecondMembersResponse))
        {
            var onlineUserNames = ReadOnlineUserNames(secondProjectPayload);
            Assert.That(onlineUserNames, Does.Not.Contain("member-c"));
        }

        var secondBoardResponse = await memberClient.GetAsync($"/api/projects/{secondProject.Id}/board");
        Assert.That(secondBoardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var afterSwitchFirstMembersResponse = await firstOwnerClient.GetAsync($"/api/projects/{firstProject.Id}/members");
        var afterSwitchSecondMembersResponse = await secondOwnerClient.GetAsync($"/api/projects/{secondProject.Id}/members");

        Assert.That(afterSwitchFirstMembersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(afterSwitchSecondMembersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var firstProjectPayload = await ReadJsonDocumentAsync(afterSwitchFirstMembersResponse))
        {
            var onlineUserNames = ReadOnlineUserNames(firstProjectPayload);
            Assert.That(onlineUserNames, Does.Not.Contain("member-c"));
        }

        using var afterSwitchSecondProjectPayload = await ReadJsonDocumentAsync(afterSwitchSecondMembersResponse);
        var onlineUserNamesAfterSwitch = ReadOnlineUserNames(afterSwitchSecondProjectPayload);

        Assert.That(onlineUserNamesAfterSwitch, Does.Contain("member-c"));
    }

    [Test]
    public async Task InviteProjectMember_WithRegisteredUser_CreatesPendingInvitationWithoutGrantingAccess()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var inviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(inviteeClient);

        await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);

        var pendingInvitationsResponse = await Client.GetAsync($"/api/projects/{project.Id}/invitations");
        var pendingInvitations = await pendingInvitationsResponse.Content.ReadFromJsonAsync<ProjectInvitationListResponse>();

        Assert.That(pendingInvitationsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(pendingInvitations, Is.Not.Null);
        Assert.That(pendingInvitations!.Items, Has.Count.EqualTo(1));
        Assert.That(pendingInvitations.Items[0].Invitee, Is.EqualTo(TestUser.OwnerB.Email));
        Assert.That(pendingInvitations.Items[0].Status, Is.EqualTo("Pending"));

        var membersResponse = await Client.GetAsync($"/api/projects/{project.Id}/members");
        var members = await membersResponse.Content.ReadFromJsonAsync<ProjectMemberListResponse>();

        Assert.That(membersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(members, Is.Not.Null);
        Assert.That(members!.Items.Select(item => item.UserName), Is.EqualTo(new[] { TestUser.OwnerA.UserName }));

        var inviteeProjectsResponse = await inviteeClient.GetAsync("/api/projects");
        var inviteeProjects = await inviteeProjectsResponse.Content.ReadFromJsonAsync<ProjectListResponse>();

        Assert.That(inviteeProjectsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(inviteeProjects, Is.Not.Null);
        Assert.That(inviteeProjects!.Items, Is.Empty);

        var inviteeBoardResponse = await inviteeClient.GetAsync($"/api/projects/{project.Id}/board");

        await AssertAccessDeniedAsync(inviteeBoardResponse);
    }

    [Test]
    public async Task AcceptInvitation_GrantsProjectAccessAndRemovesPendingInvitation()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var inviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(inviteeClient);

        var createdInvitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);

        var invitationInboxResponse = await inviteeClient.GetAsync("/api/invitations");
        var invitationInbox = await invitationInboxResponse.Content.ReadFromJsonAsync<InvitationInboxResponse>();

        Assert.That(invitationInboxResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(invitationInbox, Is.Not.Null);
        Assert.That(invitationInbox!.Items.Select(item => item.Id), Does.Contain(createdInvitation!.Id));
        Assert.That(invitationInbox.Items.Select(item => item.ProjectId), Does.Contain(project.Id));

        var acceptResponse = await inviteeClient.PostAsync($"/api/invitations/{createdInvitation.Id}/accept", content: null);

        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var membersResponse = await Client.GetAsync($"/api/projects/{project.Id}/members");
        var members = await membersResponse.Content.ReadFromJsonAsync<ProjectMemberListResponse>();

        Assert.That(membersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(members, Is.Not.Null);
        Assert.That(members!.Items.Select(item => item.UserName), Does.Contain(TestUser.OwnerB.UserName));

        var pendingInvitationsResponse = await Client.GetAsync($"/api/projects/{project.Id}/invitations");
        var pendingInvitations = await pendingInvitationsResponse.Content.ReadFromJsonAsync<ProjectInvitationListResponse>();

        Assert.That(pendingInvitationsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(pendingInvitations, Is.Not.Null);
        Assert.That(pendingInvitations!.Items.Select(item => item.Id), Does.Not.Contain(createdInvitation.Id));

        var inviteeProjectsResponse = await inviteeClient.GetAsync("/api/projects");
        var inviteeProjects = await inviteeProjectsResponse.Content.ReadFromJsonAsync<ProjectListResponse>();

        Assert.That(inviteeProjectsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(inviteeProjects, Is.Not.Null);
        Assert.That(inviteeProjects!.Items.Select(item => item.Id), Does.Contain(project.Id));
        Assert.That(inviteeProjects.Items.Single(item => item.Id == project.Id).Role, Is.EqualTo("專案成員"));

        var inviteeBoardResponse = await inviteeClient.GetAsync($"/api/projects/{project.Id}/board");

        Assert.That(inviteeBoardResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task RejectInvitation_DoesNotGrantProjectAccessAndRemovesPendingInvitation()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var inviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(inviteeClient);

        var createdInvitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);

        var rejectResponse = await inviteeClient.PostAsync($"/api/invitations/{createdInvitation!.Id}/reject", content: null);

        Assert.That(rejectResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var pendingInvitationsResponse = await Client.GetAsync($"/api/projects/{project.Id}/invitations");
        var pendingInvitations = await pendingInvitationsResponse.Content.ReadFromJsonAsync<ProjectInvitationListResponse>();

        Assert.That(pendingInvitationsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(pendingInvitations, Is.Not.Null);
        Assert.That(pendingInvitations!.Items.Select(item => item.Id), Does.Not.Contain(createdInvitation.Id));

        var inviteeProjectsResponse = await inviteeClient.GetAsync("/api/projects");
        var inviteeProjects = await inviteeProjectsResponse.Content.ReadFromJsonAsync<ProjectListResponse>();

        Assert.That(inviteeProjectsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(inviteeProjects, Is.Not.Null);
        Assert.That(inviteeProjects!.Items.Select(item => item.Id), Does.Not.Contain(project.Id));

        var inviteeBoardResponse = await inviteeClient.GetAsync($"/api/projects/{project.Id}/board");

        await AssertAccessDeniedAsync(inviteeBoardResponse);
    }

    [Test]
    public async Task InviteProjectMember_WhenInviteeIsAlreadyAcceptedMember_ReturnsValidationError()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var inviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(inviteeClient);

        var createdInvitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);
        var acceptResponse = await inviteeClient.PostAsync($"/api/invitations/{createdInvitation.Id}/accept", content: null);

        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var reinviteResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        Assert.That(reinviteResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(reinviteResponse);

        Assert.That(errors, Does.ContainKey("invitee"));
        Assert.That(errors["invitee"], Does.Contain("該使用者已是目前專案成員"));
    }

    [Test]
    public async Task ProjectMembersPanelEndpoints_WhenCallerIsAcceptedMember_ReturnAccessDenied()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var memberClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(memberClient);

        var createdInvitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);
        var acceptResponse = await memberClient.PostAsync($"/api/invitations/{createdInvitation.Id}/accept", content: null);

        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var membersResponse = await memberClient.GetAsync($"/api/projects/{project.Id}/members");
        var invitationsResponse = await memberClient.GetAsync($"/api/projects/{project.Id}/invitations");
        var inviteResponse = await memberClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest("third-user@example.test"));

        await AssertAccessDeniedAsync(membersResponse);
        await AssertAccessDeniedAsync(invitationsResponse);
        await AssertAccessDeniedAsync(inviteResponse);
    }

    [Test]
    public async Task InviteProjectMember_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-invite-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-invite-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");

        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest(TestUser.OwnerB.Email));

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task InviteProjectMember_WhenInviteeIsUnknown_ReturnsValidationError()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        await EnsureKnownUserAsync(Client);

        var inviteResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/invitations",
            new CreateProjectInvitationRequest("unknown-user@example.test"));

        Assert.That(inviteResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(inviteResponse);

        Assert.That(errors, Does.ContainKey("invitee"));
        Assert.That(errors["invitee"], Does.Contain("找不到可邀請的使用者"));
    }

    [Test]
    public async Task AcceptInvitation_WhenAlreadyAccepted_ReturnsConflict()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var inviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(inviteeClient);

        var createdInvitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);

        var firstAcceptResponse = await inviteeClient.PostAsync($"/api/invitations/{createdInvitation.Id}/accept", content: null);
        var secondAcceptResponse = await inviteeClient.PostAsync($"/api/invitations/{createdInvitation.Id}/accept", content: null);

        Assert.That(firstAcceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondAcceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task RejectInvitation_WhenAlreadyRejected_ReturnsNotFound()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        using var inviteeClient = CreateAuthenticatedClient(TestUser.OwnerB);

        await EnsureKnownUserAsync(Client);
        await EnsureKnownUserAsync(inviteeClient);

        var createdInvitation = await InviteMemberAsync(project.Id, TestUser.OwnerB.Email);

        var firstRejectResponse = await inviteeClient.PostAsync($"/api/invitations/{createdInvitation.Id}/reject", content: null);
        var secondRejectResponse = await inviteeClient.PostAsync($"/api/invitations/{createdInvitation.Id}/reject", content: null);

        Assert.That(firstRejectResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondRejectResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}