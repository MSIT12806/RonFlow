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

    public UserDirectorySyncTimings SynchronizeCurrentUser(KnownUser user)
    {
        var lookupStopwatch = System.Diagnostics.Stopwatch.StartNew();
        lock (syncRoot)
        {
            _ = usersByEmail.Values.FirstOrDefault(candidate => candidate.UserId == user.UserId);
        }
        lookupStopwatch.Stop();

        var upsertStopwatch = System.Diagnostics.Stopwatch.StartNew();
        upsertStopwatch.Stop();

        var saveStopwatch = System.Diagnostics.Stopwatch.StartNew();
        lock (syncRoot)
        {
            usersByEmail[user.Email] = user;
        }
        saveStopwatch.Stop();

        return new UserDirectorySyncTimings(
            LookupElapsedMs: lookupStopwatch.Elapsed.TotalMilliseconds,
            UpsertElapsedMs: upsertStopwatch.Elapsed.TotalMilliseconds,
            SaveElapsedMs: saveStopwatch.Elapsed.TotalMilliseconds);
    }
}