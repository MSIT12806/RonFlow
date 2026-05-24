using System.Collections.Concurrent;

namespace RonFlow.Application;

public sealed class ProjectPresenceRegistry
{
    private readonly ConcurrentDictionary<string, ProjectPresenceEntry> presencesBySession = new();

    public void EnterProject(Guid userId, string userName, string sessionId, Guid projectId)
    {
        presencesBySession[sessionId] = new ProjectPresenceEntry(userId, userName, projectId);
    }

    public IReadOnlyList<ProjectOnlineUserView> GetOnlineUsers(Guid projectId)
    {
        return presencesBySession.Values
            .Where(entry => entry.ProjectId == projectId)
            .GroupBy(entry => entry.UserId)
            .Select(group => group.Last())
            .OrderBy(entry => entry.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new ProjectOnlineUserView(entry.UserName))
            .ToArray();
    }

    public void ReleaseAllOwnedBySession(string sessionId)
    {
        presencesBySession.TryRemove(sessionId, out _);
    }

    private sealed record ProjectPresenceEntry(Guid UserId, string UserName, Guid ProjectId);
}