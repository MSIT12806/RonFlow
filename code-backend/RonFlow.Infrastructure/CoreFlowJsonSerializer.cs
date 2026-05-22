using System.Text.Json;
using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Infrastructure;

internal static class CoreFlowJsonSerializer
{
    public static string Serialize(PushSubscription subscription)
    {
        return JsonSerializer.Serialize(subscription);
    }

    public static PushSubscription DeserializePushSubscription(string json)
    {
        return JsonSerializer.Deserialize<PushSubscription>(json)
            ?? throw new InvalidOperationException("Unable to deserialize push subscription.");
    }

    public static string Serialize(Project project)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("id", project.Id);
        writer.WriteString("ownerId", project.OwnerId);
        writer.WriteString("name", project.Name);
        writer.WriteString("updatedAt", project.UpdatedAt);
        writer.WritePropertyName("workflowStates");
        writer.WriteStartArray();

        foreach (var workflowState in project.WorkflowStates)
        {
            WriteWorkflowState(writer, workflowState);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static Project DeserializeProject(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return Project.Rehydrate(
            root.GetProperty("id").GetGuid(),
            root.TryGetProperty("ownerId", out var ownerIdElement) && ownerIdElement.ValueKind != JsonValueKind.Null
                ? ownerIdElement.GetGuid()
                : Guid.Empty,
            GetRequiredString(root, "name"),
            root.GetProperty("updatedAt").GetDateTimeOffset(),
            root.GetProperty("workflowStates")
                .EnumerateArray()
                .Select(ReadWorkflowState)
                .ToArray());
    }

    public static string Serialize(DomainTask task)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("id", task.Id);
        writer.WriteString("projectId", task.ProjectId);
        writer.WriteString("title", task.Title);
        writer.WriteString("description", task.Description);
        writer.WritePropertyName("currentState");
        WriteWorkflowState(writer, task.CurrentState);
        writer.WriteString("lifecycleState", task.LifecycleState.ToString());
        if (task.DueDate is null)
        {
            writer.WriteNull("dueDate");
        }
        else
        {
            writer.WriteString("dueDate", task.DueDate.Value.ToString("yyyy-MM-dd"));
        }
        writer.WriteString("createdAt", task.CreatedAt);

        if (task.CompletedAt is null)
        {
            writer.WriteNull("completedAt");
        }
        else
        {
            writer.WriteString("completedAt", task.CompletedAt.Value);
        }

        if (task.ArchivedAt is null)
        {
            writer.WriteNull("archivedAt");
        }
        else
        {
            writer.WriteString("archivedAt", task.ArchivedAt.Value);
        }

        if (task.TrashedAt is null)
        {
            writer.WriteNull("trashedAt");
        }
        else
        {
            writer.WriteString("trashedAt", task.TrashedAt.Value);
        }

        writer.WriteNumber("sortOrder", task.SortOrder);

        writer.WritePropertyName("reminders");
        writer.WriteStartArray();

        foreach (var reminder in task.Reminders)
        {
            writer.WriteStartObject();
            writer.WriteString("id", reminder.Id);
            writer.WriteString("reminderDateTime", reminder.ReminderDateTime);
            writer.WriteString("description", reminder.Description);
            if (reminder.NotificationDispatchedAt is null)
            {
                writer.WriteNull("notificationDispatchedAt");
            }
            else
            {
                writer.WriteString("notificationDispatchedAt", reminder.NotificationDispatchedAt.Value);
            }
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("activityTimeline");
        writer.WriteStartArray();

        foreach (var item in task.ActivityTimeline)
        {
            WriteActivityTimelineItem(writer, item);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static DomainTask DeserializeTask(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var completedAt = root.TryGetProperty("completedAt", out var completedAtElement) && completedAtElement.ValueKind != JsonValueKind.Null
            ? completedAtElement.GetDateTimeOffset()
            : (DateTimeOffset?)null;
        var archivedAt = root.TryGetProperty("archivedAt", out var archivedAtElement) && archivedAtElement.ValueKind != JsonValueKind.Null
            ? archivedAtElement.GetDateTimeOffset()
            : (DateTimeOffset?)null;
        var trashedAt = root.TryGetProperty("trashedAt", out var trashedAtElement) && trashedAtElement.ValueKind != JsonValueKind.Null
            ? trashedAtElement.GetDateTimeOffset()
            : (DateTimeOffset?)null;
        var description = root.TryGetProperty("description", out var descriptionElement) && descriptionElement.ValueKind != JsonValueKind.Null
            ? descriptionElement.GetString() ?? string.Empty
            : string.Empty;
        var dueDate = root.TryGetProperty("dueDate", out var dueDateElement) && dueDateElement.ValueKind != JsonValueKind.Null
            ? DateOnly.Parse(GetRequiredString(root, "dueDate"))
            : (DateOnly?)null;
        var lifecycleState = root.TryGetProperty("lifecycleState", out var lifecycleStateElement)
            && Enum.TryParse<TaskLifecycleState>(lifecycleStateElement.GetString(), out var parsedLifecycleState)
                ? parsedLifecycleState
                : TaskLifecycleState.ActiveRecord;
        var sortOrder = root.TryGetProperty("sortOrder", out var sortOrderElement)
            ? sortOrderElement.GetInt32()
            : 0;
        var reminders = root.TryGetProperty("reminders", out var remindersElement) && remindersElement.ValueKind != JsonValueKind.Null
            ? remindersElement
                .EnumerateArray()
                .Select(element => new TaskReminder(
                    element.GetProperty("id").GetGuid(),
                    GetRequiredString(element, "reminderDateTime"),
                    element.TryGetProperty("description", out var reminderDescriptionElement) && reminderDescriptionElement.ValueKind != JsonValueKind.Null
                        ? reminderDescriptionElement.GetString() ?? string.Empty
                        : string.Empty,
                    element.TryGetProperty("notificationDispatchedAt", out var reminderDispatchedAtElement)
                        && reminderDispatchedAtElement.ValueKind != JsonValueKind.Null
                            ? reminderDispatchedAtElement.GetDateTimeOffset()
                            : (DateTimeOffset?)null))
                .ToArray()
            : [];

        return DomainTask.Rehydrate(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("projectId").GetGuid(),
            GetRequiredString(root, "title"),
            description,
            ReadWorkflowState(root.GetProperty("currentState")),
            lifecycleState,
            dueDate,
            root.GetProperty("createdAt").GetDateTimeOffset(),
            completedAt,
            archivedAt,
            trashedAt,
            sortOrder,
            reminders,
            root.GetProperty("activityTimeline")
                .EnumerateArray()
                .Select(ReadActivityTimelineItem)
                .ToArray());
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"Property '{propertyName}' is required.");
    }

    private static WorkflowState ReadWorkflowState(JsonElement element)
    {
        var key = GetRequiredString(element, "key");

        return new WorkflowState(
            key,
            GetRequiredString(element, "label"),
            element.GetProperty("isInitialState").GetBoolean(),
            element.TryGetProperty("isCompletedState", out var isCompletedState)
                ? isCompletedState.GetBoolean()
                : key == "done");
    }

    private static ActivityTimelineItem ReadActivityTimelineItem(JsonElement element)
    {
        return new ActivityTimelineItem(
            GetRequiredString(element, "type"),
            GetRequiredString(element, "message"),
            element.GetProperty("occurredAt").GetDateTimeOffset());
    }

    private static void WriteWorkflowState(Utf8JsonWriter writer, WorkflowState workflowState)
    {
        writer.WriteStartObject();
        writer.WriteString("key", workflowState.Key);
        writer.WriteString("label", workflowState.Label);
        writer.WriteBoolean("isInitialState", workflowState.IsInitialState);
        writer.WriteBoolean("isCompletedState", workflowState.IsCompletedState);
        writer.WriteEndObject();
    }

    private static void WriteActivityTimelineItem(Utf8JsonWriter writer, ActivityTimelineItem item)
    {
        writer.WriteStartObject();
        writer.WriteString("type", item.Type);
        writer.WriteString("message", item.Message);
        writer.WriteString("occurredAt", item.OccurredAt);
        writer.WriteEndObject();
    }
}