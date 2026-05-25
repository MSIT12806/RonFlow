namespace RonFlow.Domain;

public sealed record KnownUser(Guid UserId, string UserName, string Email);

public readonly record struct UserDirectorySyncTimings(
    double LookupElapsedMs,
    double UpsertElapsedMs,
    double SaveElapsedMs);

public interface IUserDirectory
{
    KnownUser? FindByEmail(string email);

    void Upsert(KnownUser user);

    UserDirectorySyncTimings SynchronizeCurrentUser(KnownUser user);
}