using RonFlow.Domain;

namespace RonFlow.Testing.Infrastructure;

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

    public UserDirectorySyncTimings SynchronizeCurrentUser(KnownUser user)
    {
        var lookupStopwatch = System.Diagnostics.Stopwatch.StartNew();
        KnownUser? existingUser;
        lock (syncRoot)
        {
            existingUser = usersByEmail.Values.FirstOrDefault(candidate => candidate.UserId == user.UserId);
        }
        lookupStopwatch.Stop();

        var upsertStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var requiresSave = existingUser is null
            || existingUser.UserName != user.UserName
            || existingUser.Email != user.Email;
        upsertStopwatch.Stop();

        var saveStopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (requiresSave)
        {
            lock (syncRoot)
            {
                if (existingUser is not null && !string.Equals(existingUser.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    usersByEmail.Remove(existingUser.Email);
                }

                usersByEmail[user.Email] = user;
            }
        }
        saveStopwatch.Stop();

        return new UserDirectorySyncTimings(
            LookupElapsedMs: lookupStopwatch.Elapsed.TotalMilliseconds,
            UpsertElapsedMs: upsertStopwatch.Elapsed.TotalMilliseconds,
            SaveElapsedMs: saveStopwatch.Elapsed.TotalMilliseconds);
    }
}
