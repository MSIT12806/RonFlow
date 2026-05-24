namespace RonFlow.Domain;

public enum ProjectInvitationStatus
{
    Pending,
    Accepted,
    Rejected,
}

public sealed record ProjectMember(Guid UserId, string UserName, string Email)
{
    public bool MatchesUser(Guid userId)
    {
        return UserId == userId;
    }

    public bool MatchesEmail(string email)
    {
        return string.Equals(Email, email, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ProjectInvitation
{
    private ProjectInvitation(
        Guid id,
        string inviteeEmail,
        Guid inviterUserId,
        string inviterName,
        DateTimeOffset createdAt,
        ProjectInvitationStatus status,
        DateTimeOffset? respondedAt)
    {
        Id = id;
        InviteeEmail = inviteeEmail;
        InviterUserId = inviterUserId;
        InviterName = inviterName;
        CreatedAt = createdAt;
        Status = status;
        RespondedAt = respondedAt;
    }

    public Guid Id { get; }

    public string InviteeEmail { get; }

    public Guid InviterUserId { get; }

    public string InviterName { get; }

    public DateTimeOffset CreatedAt { get; }

    public ProjectInvitationStatus Status { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public bool IsPending => Status == ProjectInvitationStatus.Pending;

    public static ProjectInvitation Create(string inviteeEmail, Guid inviterUserId, string inviterName, DateTimeOffset createdAt)
    {
        return new(Guid.NewGuid(), inviteeEmail, inviterUserId, inviterName, createdAt, ProjectInvitationStatus.Pending, null);
    }

    public static ProjectInvitation Rehydrate(
        Guid id,
        string inviteeEmail,
        Guid inviterUserId,
        string inviterName,
        DateTimeOffset createdAt,
        ProjectInvitationStatus status,
        DateTimeOffset? respondedAt)
    {
        return new(id, inviteeEmail, inviterUserId, inviterName, createdAt, status, respondedAt);
    }

    public bool IsPendingFor(string email)
    {
        return IsPending && MatchesInvitee(email);
    }

    public bool MatchesInvitee(string email)
    {
        return string.Equals(InviteeEmail, email, StringComparison.OrdinalIgnoreCase);
    }

    public void Accept(DateTimeOffset respondedAt)
    {
        Status = ProjectInvitationStatus.Accepted;
        RespondedAt = respondedAt;
    }

    public void Reject(DateTimeOffset respondedAt)
    {
        Status = ProjectInvitationStatus.Rejected;
        RespondedAt = respondedAt;
    }
}