using System.Collections.Concurrent;

namespace RonFlow.Application;

public sealed class TaskContentEditLockService
{
    private readonly ConcurrentDictionary<Guid, TaskContentEditLockEntry> locks = new();

    public bool IsHeldBy(Guid currentUserId, Guid taskId)
    {
        return locks.TryGetValue(taskId, out var activeLock) && activeLock.UserId == currentUserId;
    }

    public bool IsHeldByAnotherUser(Guid currentUserId, Guid taskId)
    {
        return locks.TryGetValue(taskId, out var activeLock) && activeLock.UserId != currentUserId;
    }

    public bool IsLocked(Guid taskId)
    {
        return locks.ContainsKey(taskId);
    }

    public bool CanEnterEdit(Guid currentUserId, Guid taskId)
    {
        return !locks.TryGetValue(taskId, out var activeLock) || activeLock.UserId == currentUserId;
    }

    public bool TryAcquire(Guid currentUserId, string currentUserName, string sessionId, Guid taskId)
    {
        while (true)
        {
            if (locks.TryGetValue(taskId, out var activeLock))
            {
                return activeLock.UserId == currentUserId && activeLock.SessionId == sessionId;
            }

            var nextLock = new TaskContentEditLockEntry(currentUserId, currentUserName, sessionId);
            if (locks.TryAdd(taskId, nextLock))
            {
                return true;
            }
        }
    }

    public void ReleaseIfOwned(Guid currentUserId, Guid taskId)
    {
        if (!locks.TryGetValue(taskId, out var activeLock) || activeLock.UserId != currentUserId)
        {
            return;
        }

        locks.TryRemove(new KeyValuePair<Guid, TaskContentEditLockEntry>(taskId, activeLock));
    }

    public void ReleaseAllOwnedBySession(string sessionId)
    {
        foreach (var activeLock in locks)
        {
            if (activeLock.Value.SessionId != sessionId)
            {
                continue;
            }

            locks.TryRemove(new KeyValuePair<Guid, TaskContentEditLockEntry>(activeLock.Key, activeLock.Value));
        }
    }

    private sealed record TaskContentEditLockEntry(Guid UserId, string UserName, string SessionId);
}