using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RonFlow.Api.Contracts;
using RonFlow.Application;
using RonFlow.Infrastructure;

namespace RonFlow.Api;

[Authorize]
public sealed class DatabaseSyncNotificationHub(RonFlowActiveSessionRegistry activeSessionRegistry) : Hub
{
    public const string Route = "/hubs/database-sync-notifications";

    public Task RegisterSession(string sessionId)
    {
        if (!TryGetCurrentUserId(Context.User, out var userId) ||
            string.IsNullOrWhiteSpace(sessionId) ||
            !activeSessionRegistry.IsCurrentActiveSession(userId, sessionId))
        {
            throw new HubException("RonFlow session is not active.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, CreateGroupName(userId, sessionId));
    }

    internal static string CreateGroupName(Guid userId, string sessionId)
    {
        return $"database-sync:{userId:N}:{sessionId}";
    }

    private static bool TryGetCurrentUserId(ClaimsPrincipal? user, out Guid userId)
    {
        var rawUserId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out userId);
    }
}

public sealed class SignalRDatabaseSyncNotificationPublisher(
    IHubContext<DatabaseSyncNotificationHub> hubContext,
    RonFlowActiveSessionRegistry activeSessionRegistry,
    ILogger<SignalRDatabaseSyncNotificationPublisher> logger) : IDatabaseSyncNotificationPublisher
{
    public void Publish(DatabaseSyncNotification notification)
    {
        var operation = notification.Operation;
        if (!activeSessionRegistry.TryGetActiveSession(operation.InitiatorUserId, out var sessionId))
        {
            return;
        }

        var groupName = DatabaseSyncNotificationHub.CreateGroupName(operation.InitiatorUserId, sessionId);
        try
        {
            hubContext.Clients
                .Group(groupName)
                .SendAsync("databaseSyncCompleted", DatabaseSyncOperationResponse.FromOperation(operation))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to send database sync notification. OperationId: {OperationId}; InitiatorUserId: {InitiatorUserId}",
                operation.Id,
                operation.InitiatorUserId);
        }
    }
}

public sealed class SignalRTaskNotificationPublisher(
    IHubContext<DatabaseSyncNotificationHub> hubContext,
    RonFlowActiveSessionRegistry activeSessionRegistry,
    ILogger<SignalRTaskNotificationPublisher> logger) : ITaskNotificationPublisher
{
    public bool Publish(TaskNotificationSource notification)
    {
        if (notification.RecipientUserId == Guid.Empty ||
            !activeSessionRegistry.TryGetActiveSession(notification.RecipientUserId, out var sessionId))
        {
            return false;
        }

        var groupName = DatabaseSyncNotificationHub.CreateGroupName(notification.RecipientUserId, sessionId);
        try
        {
            hubContext.Clients
                .Group(groupName)
                .SendAsync("taskNotification", TaskNotificationResponse.FromSource(notification))
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to send task notification. MessageId: {MessageId}; RecipientUserId: {RecipientUserId}",
                notification.MessageId,
                notification.RecipientUserId);
            return false;
        }
    }
}

public sealed class HttpContextDatabaseSyncInitiatorContext(IHttpContextAccessor httpContextAccessor) : IDatabaseSyncInitiatorContext
{
    public DatabaseSyncInitiator? GetCurrent()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var rawUserId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var userId)
            ? new DatabaseSyncInitiator(userId)
            : null;
    }
}
