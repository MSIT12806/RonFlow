namespace RonFlow.Domain;

public sealed record KnownUser(Guid UserId, string UserName, string Email);

public interface IUserDirectory
{
    KnownUser? FindByEmail(string email);

    void Upsert(KnownUser user);
}