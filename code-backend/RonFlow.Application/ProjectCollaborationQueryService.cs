using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ProjectCollaborationQueryService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ProjectPresenceRegistry projectPresenceRegistry)
{
    public OwnedResourceQueryResult<ProjectMemberListView> GetMembers(Guid currentUserId, Guid projectId)
    {
        var access = projectAccessService.GetOwnerProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<ProjectMemberListView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<ProjectMemberListView>.Denied();
        }

        var project = access.Project!;
        var members = project.GetAllMembers()
            .Select(member => new ProjectMemberView(
                member.UserName,
                member.UserId == project.OwnerId ? "專案擁有者" : "專案成員"))
            .ToArray();

        var onlineUsers = projectPresenceRegistry.GetOnlineUsers(project.Id);

        return OwnedResourceQueryResult<ProjectMemberListView>.Success(new ProjectMemberListView(members, onlineUsers));
    }

    public OwnedResourceQueryResult<ProjectInvitationListView> GetPendingInvitations(Guid currentUserId, Guid projectId)
    {
        var access = projectAccessService.GetOwnerProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<ProjectInvitationListView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<ProjectInvitationListView>.Denied();
        }

        var invitations = access.Project!
            .GetPendingInvitations()
            .Select(invitation => new ProjectInvitationView(invitation.Id, invitation.InviteeEmail, invitation.Status.ToString()))
            .ToArray();

        return OwnedResourceQueryResult<ProjectInvitationListView>.Success(new ProjectInvitationListView(invitations));
    }

    public InvitationInboxView GetInvitationInbox(string currentUserEmail)
    {
        var items = projectRepository.GetAll()
            .SelectMany(project => project.GetPendingInvitations()
                .Where(invitation => invitation.IsPendingFor(currentUserEmail))
                .Select(invitation => new InvitationInboxItemView(invitation.Id, project.Id, project.Name, invitation.InviterName)))
            .OrderBy(item => item.ProjectName)
            .ToArray();

        return new InvitationInboxView(items);
    }
}