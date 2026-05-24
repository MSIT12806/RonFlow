using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ProjectInvitationCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    IUserDirectory userDirectory,
    TimeProvider timeProvider)
{
    public CreateProjectInvitationResult Invite(Guid currentUserId, string currentUserName, Guid projectId, string? rawInviteeEmail)
    {
        if (string.IsNullOrWhiteSpace(rawInviteeEmail))
        {
            return CreateProjectInvitationResult.Invalid("invitee", "邀請對象為必填欄位");
        }

        var inviteeEmail = rawInviteeEmail.Trim();
        var access = projectAccessService.GetOwnerProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return CreateProjectInvitationResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return CreateProjectInvitationResult.Denied();
        }

        var project = access.Project!;
        if (userDirectory.FindByEmail(inviteeEmail) is null)
        {
            return CreateProjectInvitationResult.Invalid("invitee", "找不到可邀請的使用者");
        }

        if (project.HasMemberWithEmail(inviteeEmail))
        {
            return CreateProjectInvitationResult.Invalid("invitee", "該使用者已是目前專案成員");
        }

        if (project.HasPendingInvitationFor(inviteeEmail))
        {
            return CreateProjectInvitationResult.Invalid("invitee", "該使用者已有待處理邀請");
        }

        var invitation = project.AddInvitation(inviteeEmail, currentUserId, currentUserName, timeProvider.GetUtcNow());
        projectRepository.Update(project);

        return CreateProjectInvitationResult.Success(
            new ProjectInvitationView(invitation.Id, invitation.InviteeEmail, invitation.Status.ToString()));
    }

    public AcceptProjectInvitationResult Accept(Guid currentUserId, string currentUserName, string currentUserEmail, Guid invitationId)
    {
        if (string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return AcceptProjectInvitationResult.Denied();
        }

        var project = projectRepository.GetAll()
            .FirstOrDefault(candidate => candidate.FindInvitation(invitationId) is not null);

        if (project is null)
        {
            return AcceptProjectInvitationResult.NotFound();
        }

        var invitation = project.FindInvitation(invitationId);
        if (invitation is null || !invitation.MatchesInvitee(currentUserEmail))
        {
            return AcceptProjectInvitationResult.Denied();
        }

        if (!invitation.IsPending)
        {
            return AcceptProjectInvitationResult.Conflict();
        }

        project.AcceptInvitation(invitationId, currentUserId, currentUserName, currentUserEmail, timeProvider.GetUtcNow());
        projectRepository.Update(project);
        return AcceptProjectInvitationResult.Success();
    }

    public RespondToProjectInvitationResult Reject(Guid currentUserEmailOwnerId, string currentUserName, string currentUserEmail, Guid invitationId)
    {
        return Respond(currentUserEmailOwnerId, currentUserName, currentUserEmail, invitationId, accept: false);
    }

    private RespondToProjectInvitationResult Respond(
        Guid currentUserId,
        string currentUserName,
        string currentUserEmail,
        Guid invitationId,
        bool accept)
    {
        if (string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return RespondToProjectInvitationResult.Denied();
        }

        var project = projectRepository.GetAll()
            .FirstOrDefault(candidate => candidate.FindInvitation(invitationId) is not null);

        if (project is null)
        {
            return RespondToProjectInvitationResult.NotFound();
        }

        var invitation = project.FindInvitation(invitationId);
        if (invitation is null || !invitation.MatchesInvitee(currentUserEmail))
        {
            return RespondToProjectInvitationResult.Denied();
        }

        if (!invitation.IsPending)
        {
            return RespondToProjectInvitationResult.NotFound();
        }

        var respondedAt = timeProvider.GetUtcNow();
        if (accept)
        {
            project.AcceptInvitation(invitationId, currentUserId, currentUserName, currentUserEmail, respondedAt);
        }
        else
        {
            project.RejectInvitation(invitationId, currentUserEmail, respondedAt);
        }

        projectRepository.Update(project);
        return RespondToProjectInvitationResult.Success();
    }
}