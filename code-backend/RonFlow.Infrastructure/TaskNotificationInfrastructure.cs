using RonFlow.Application;
using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class SqliteTaskNotificationOutbox(SqliteCoreFlowStore store) : ITaskNotificationOutbox
{
    public void EnqueueWorkflowStateChanged(TaskWorkflowStateChangedDomainEvent domainEvent)
    {
        Add(new TaskNotificationSource(
            Guid.NewGuid(),
            domainEvent.ActorUserId,
            domainEvent.ProjectId,
            domainEvent.TaskId,
            "TaskWorkflowStateChanged",
            domainEvent.TaskTitle,
            domainEvent.StateKey,
            domainEvent.StateLabel,
            domainEvent.OccurredAt,
            null));
    }

    public void EnqueueMovedToTrash(TaskMovedToTrashDomainEvent domainEvent)
    {
        Add(new TaskNotificationSource(
            Guid.NewGuid(),
            domainEvent.ActorUserId,
            domainEvent.ProjectId,
            domainEvent.TaskId,
            "TaskMovedToTrash",
            domainEvent.TaskTitle,
            null,
            null,
            domainEvent.OccurredAt,
            null));
    }

    public IReadOnlyList<TaskNotificationSource> GetPending()
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT MessageId, RecipientUserId, ProjectId, TaskId, EventType, TaskTitle, StateKey, StateLabel, OccurredAt, ProcessedAt
FROM TaskNotificationOutbox
WHERE ProcessedAt IS NULL
ORDER BY OccurredAt";

        using var reader = command.ExecuteReader();
        var items = new List<TaskNotificationSource>();
        while (reader.Read())
        {
            items.Add(new TaskNotificationSource(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                Guid.Parse(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                DateTimeOffset.Parse(reader.GetString(8)),
                reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9))));
        }

        return items;
    }

    public void MarkProcessed(Guid messageId, DateTimeOffset processedAt)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE TaskNotificationOutbox SET ProcessedAt = $processedAt WHERE MessageId = $messageId";
        command.Parameters.AddWithValue("$processedAt", processedAt.ToString("O"));
        command.Parameters.AddWithValue("$messageId", messageId.ToString());
        if (command.ExecuteNonQuery() > 0)
        {
            store.NotifyChanged("task notification outbox processed");
        }
    }

    private void Add(TaskNotificationSource notification)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO TaskNotificationOutbox (MessageId, RecipientUserId, ProjectId, TaskId, EventType, TaskTitle, StateKey, StateLabel, OccurredAt, ProcessedAt)
VALUES ($messageId, $recipientUserId, $projectId, $taskId, $eventType, $taskTitle, $stateKey, $stateLabel, $occurredAt, NULL)";
        command.Parameters.AddWithValue("$messageId", notification.MessageId.ToString());
        command.Parameters.AddWithValue("$recipientUserId", notification.RecipientUserId.ToString());
        command.Parameters.AddWithValue("$projectId", notification.ProjectId.ToString());
        command.Parameters.AddWithValue("$taskId", notification.TaskId.ToString());
        command.Parameters.AddWithValue("$eventType", notification.EventType);
        command.Parameters.AddWithValue("$taskTitle", notification.TaskTitle);
        command.Parameters.AddWithValue("$stateKey", (object?)notification.StateKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$stateLabel", (object?)notification.StateLabel ?? DBNull.Value);
        command.Parameters.AddWithValue("$occurredAt", notification.OccurredAt.ToString("O"));
        if (command.ExecuteNonQuery() > 0)
        {
            store.NotifyChanged("task notification outbox enqueued");
        }
    }
}
