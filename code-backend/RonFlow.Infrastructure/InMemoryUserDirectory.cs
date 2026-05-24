using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class InMemoryUserDirectory : IUserDirectory
{
    private readonly object syncRoot = new();
    private readonly Dictionary<string, KnownUser> usersByEmail = new(StringComparer.OrdinalIgnoreCase);

    public KnownUser? FindByEmail(string email)
    {
        lock (syncRoot)
        {
            return usersByEmail.GetValueOrDefault(email);
        }
    }

    public void Upsert(KnownUser user)
    {
        lock (syncRoot)
        {
            usersByEmail[user.Email] = user;
        }
    }
}