using System.Net;
using System.Net.Http.Json;
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