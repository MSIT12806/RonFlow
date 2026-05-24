namespace RonFlow.Domain;

public sealed class Project
{
    private readonly IReadOnlyList<WorkflowState> workflowStates;
    private readonly List<ProjectMember> members;
    private readonly List<ProjectInvitation> invitations;

    private Project(
        Guid id,
        Guid ownerId,
        string ownerUserName,
        string ownerEmail,
        string name,
        DateTimeOffset updatedAt,
        IEnumerable<WorkflowState> workflowStates,
        IEnumerable<ProjectMember> members,
        IEnumerable<ProjectInvitation> invitations)
    {
        Id = id;
        OwnerId = ownerId;
        OwnerUserName = ownerUserName;
        OwnerEmail = ownerEmail;
        Name = name;
        UpdatedAt = updatedAt;
        this.workflowStates = workflowStates
            .Select(state => new WorkflowState(state.Key, state.Label, state.IsInitialState, state.IsCompletedState))
            .ToArray();
        this.members = members.ToList();
        this.invitations = invitations.ToList();
    }

    public Guid Id { get; }

    public Guid OwnerId { get; }

    public string OwnerUserName { get; }

    public string OwnerEmail { get; }

    public string Name { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<WorkflowState> WorkflowStates => workflowStates;

    public IReadOnlyList<ProjectMember> Members => members;

    public IReadOnlyList<ProjectInvitation> Invitations => invitations;

    public static Project Create(Guid ownerId, ProjectName name, DateTimeOffset createdAt, IEnumerable<WorkflowState> workflowStates)
    {
        return Create(ownerId, string.Empty, string.Empty, name, createdAt, workflowStates);
    }

    public static Project Create(
        Guid ownerId,
        string ownerUserName,
        string ownerEmail,
        ProjectName name,
        DateTimeOffset createdAt,
        IEnumerable<WorkflowState> workflowStates)
    {
        return new Project(Guid.NewGuid(), ownerId, ownerUserName, ownerEmail, name.Value, createdAt, workflowStates, [], []);
    }

    public static Project Create(ProjectName name, DateTimeOffset createdAt, IEnumerable<WorkflowState> workflowStates)
    {
        return Create(Guid.Empty, string.Empty, string.Empty, name, createdAt, workflowStates);
    }

    public static Project Rehydrate(Guid id, Guid ownerId, string name, DateTimeOffset updatedAt, IEnumerable<WorkflowState> workflowStates)
    {
        return Rehydrate(id, ownerId, string.Empty, string.Empty, name, updatedAt, workflowStates, [], []);
    }

    public static Project Rehydrate(
        Guid id,
        Guid ownerId,
        string ownerUserName,
        string ownerEmail,
        string name,
        DateTimeOffset updatedAt,
        IEnumerable<WorkflowState> workflowStates,
        IEnumerable<ProjectMember> members,
        IEnumerable<ProjectInvitation> invitations)
    {
        return new Project(id, ownerId, ownerUserName, ownerEmail, name, updatedAt, workflowStates, members, invitations);
    }

    public ProjectModel ToModel()
    {
        return new ProjectModel(Id, OwnerId, Name, UpdatedAt, workflowStates.Select(state => state.ToModel()).ToArray());
    }

    public ProjectSummaryModel ToSummaryModel()
    {
        return new ProjectSummaryModel(Id, OwnerId, Name, UpdatedAt);
    }

    public bool IsOwnedBy(Guid userId)
    {
        return OwnerId == userId;
    }

    public bool IsAccessibleBy(Guid userId)
    {
        return IsOwnedBy(userId) || members.Any(member => member.MatchesUser(userId));
    }

    public bool HasMemberWithEmail(string email)
    {
        return string.Equals(OwnerEmail, email, StringComparison.OrdinalIgnoreCase)
            || members.Any(member => member.MatchesEmail(email));
    }

    public bool HasPendingInvitationFor(string email)
    {
        return invitations.Any(invitation => invitation.IsPending && invitation.MatchesInvitee(email));
    }

    public ProjectInvitation AddInvitation(string inviteeEmail, Guid inviterUserId, string inviterUserName, DateTimeOffset createdAt)
    {
        var invitation = ProjectInvitation.Create(inviteeEmail, inviterUserId, inviterUserName, createdAt);
        invitations.Add(invitation);
        Touch(createdAt);
        return invitation;
    }

    public ProjectInvitation? FindPendingInvitation(Guid invitationId)
    {
        return invitations.FirstOrDefault(invitation => invitation.Id == invitationId && invitation.IsPending);
    }

    public ProjectInvitation? FindInvitation(Guid invitationId)
    {
        return invitations.FirstOrDefault(invitation => invitation.Id == invitationId);
    }

    public IReadOnlyList<ProjectMember> GetAllMembers()
    {
        var owner = new ProjectMember(OwnerId, OwnerUserName, OwnerEmail);
        return [owner, .. members];
    }

    public IReadOnlyList<ProjectInvitation> GetPendingInvitations()
    {
        return invitations.Where(invitation => invitation.IsPending).OrderBy(invitation => invitation.CreatedAt).ToArray();
    }

    public void AcceptInvitation(Guid invitationId, Guid userId, string userName, string email, DateTimeOffset respondedAt)
    {
        var invitation = FindPendingInvitation(invitationId)
            ?? throw new InvalidOperationException("Invitation was not found.");

        if (!invitation.MatchesInvitee(email))
        {
            throw new InvalidOperationException("Invitation does not belong to the current user.");
        }

        invitation.Accept(respondedAt);

        if (!members.Any(member => member.MatchesUser(userId)))
        {
            members.Add(new ProjectMember(userId, userName, email));
        }

        Touch(respondedAt);
    }

    public void RejectInvitation(Guid invitationId, string email, DateTimeOffset respondedAt)
    {
        var invitation = FindPendingInvitation(invitationId)
            ?? throw new InvalidOperationException("Invitation was not found.");

        if (!invitation.MatchesInvitee(email))
        {
            throw new InvalidOperationException("Invitation does not belong to the current user.");
        }

        invitation.Reject(respondedAt);
        Touch(respondedAt);
    }

    /// <summary>
    /// 取得這個 Project Task 建立的預設 WorkflowState
    /// </summary>
    public WorkflowState GetDefaultWorkflowState()
    {
        var initialState = workflowStates.First(state => state.IsInitialState);
        return initialState;
    }

    public WorkflowState GetWorkflowState(string stateKey)
    {
        return workflowStates.First(state => state.Key == stateKey);
    }

    public WorkflowState? FindWorkflowState(string stateKey)
    {
        return workflowStates.FirstOrDefault(state => state.Key == stateKey);
    }

    public void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}