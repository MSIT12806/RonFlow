using System.Collections.Concurrent;

namespace RonFlow.Application;

public sealed class RonFlowActiveSessionRegistry(
    TaskContentEditLockService taskContentEditLockService,
    ProjectPresenceRegistry projectPresenceRegistry)
{
    private readonly ConcurrentDictionary<Guid, string> activeSessions = new();

    public void Activate(Guid userId, string sessionId)
    {
        while (true)
        {
            if (!activeSessions.TryGetValue(userId, out var currentSessionId))
            {
                if (activeSessions.TryAdd(userId, sessionId))
                {
                    return;
                }

                continue;
            }

            if (currentSessionId == sessionId)
            {
                return;
            }

            if (activeSessions.TryUpdate(userId, sessionId, currentSessionId))
            {
                taskContentEditLockService.ReleaseAllOwnedBySession(currentSessionId);
                projectPresenceRegistry.ReleaseAllOwnedBySession(currentSessionId);
                return;
            }
        }
    }

    public bool IsActive(Guid userId, string sessionId)
    {
        return !activeSessions.TryGetValue(userId, out var currentSessionId) || currentSessionId == sessionId;
    }

    public void ReleaseProjectScope(string sessionId)
    {
        taskContentEditLockService.ReleaseAllOwnedBySession(sessionId);
        projectPresenceRegistry.ReleaseAllOwnedBySession(sessionId);
    }
}