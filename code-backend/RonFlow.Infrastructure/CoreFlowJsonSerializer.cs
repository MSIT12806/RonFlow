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
        writer.WriteString("ownerUserName", project.OwnerUserName);
        writer.WriteString("ownerEmail", project.OwnerEmail);
        writer.WriteString("name", project.Name);
        writer.WriteString("updatedAt", project.UpdatedAt);
        writer.WritePropertyName("workflowStates");
        writer.WriteStartArray();

        foreach (var workflowState in project.WorkflowStates)
        {
            WriteWorkflowState(writer, workflowState);
        }

        writer.WriteEndArray();

        writer.WritePropertyName("subtaskTemplates");
        writer.WriteStartArray();

        foreach (var template in project.SubtaskTemplates)
        {
            WriteProjectSubtaskTemplate(writer, template);
        }

        writer.WriteEndArray();

        writer.WritePropertyName("members");
        writer.WriteStartArray();

        foreach (var member in project.Members)
        {
            writer.WriteStartObject();
            writer.WriteString("userId", member.UserId);
            writer.WriteString("userName", member.UserName);
            writer.WriteString("email", member.Email);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("invitations");
        writer.WriteStartArray();

        foreach (var invitation in project.Invitations)
        {
            writer.WriteStartObject();
            writer.WriteString("id", invitation.Id);
            writer.WriteString("inviteeEmail", invitation.InviteeEmail);
            writer.WriteString("inviterUserId", invitation.InviterUserId);
            writer.WriteString("inviterName", invitation.InviterName);
            writer.WriteString("createdAt", invitation.CreatedAt);
            writer.WriteString("status", invitation.Status.ToString());
            if (invitation.RespondedAt is null)
            {
                writer.WriteNull("respondedAt");
            }
            else
            {
                writer.WriteString("respondedAt", invitation.RespondedAt.Value);
            }
            writer.WriteEndObject();
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
            root.TryGetProperty("ownerUserName", out var ownerUserNameElement) && ownerUserNameElement.ValueKind != JsonValueKind.Null
                ? ownerUserNameElement.GetString() ?? string.Empty
                : string.Empty,
            root.TryGetProperty("ownerEmail", out var ownerEmailElement) && ownerEmailElement.ValueKind != JsonValueKind.Null
                ? ownerEmailElement.GetString() ?? string.Empty
                : string.Empty,
            GetRequiredString(root, "name"),
            root.GetProperty("updatedAt").GetDateTimeOffset(),
            root.GetProperty("workflowStates")
                .EnumerateArray()
                .Select(ReadWorkflowState)
                .ToArray(),
            root.TryGetProperty("subtaskTemplates", out var subtaskTemplatesElement) && subtaskTemplatesElement.ValueKind != JsonValueKind.Null
                ? subtaskTemplatesElement
                    .EnumerateArray()
                    .Select(ReadProjectSubtaskTemplate)
                    .ToArray()
                : [],
            root.TryGetProperty("members", out var membersElement) && membersElement.ValueKind != JsonValueKind.Null
                ? membersElement
                    .EnumerateArray()
                    .Select(element => new ProjectMember(
                        element.GetProperty("userId").GetGuid(),
                        GetRequiredString(element, "userName"),
                        GetRequiredString(element, "email")))
                    .ToArray()
                : [],
            root.TryGetProperty("invitations", out var invitationsElement) && invitationsElement.ValueKind != JsonValueKind.Null
                ? invitationsElement
                    .EnumerateArray()
                    .Select(element => ProjectInvitation.Rehydrate(
                        element.GetProperty("id").GetGuid(),
                        GetRequiredString(element, "inviteeEmail"),
                        element.TryGetProperty("inviterUserId", out var inviterUserIdElement) && inviterUserIdElement.ValueKind != JsonValueKind.Null
                            ? inviterUserIdElement.GetGuid()
                            : Guid.Empty,
                        element.TryGetProperty("inviterName", out var inviterNameElement) && inviterNameElement.ValueKind != JsonValueKind.Null
                            ? inviterNameElement.GetString() ?? string.Empty
                            : string.Empty,
                        element.GetProperty("createdAt").GetDateTimeOffset(),
                        element.TryGetProperty("status", out var statusElement)
                            && Enum.TryParse<ProjectInvitationStatus>(statusElement.GetString(), ignoreCase: true, out var status)
                                ? status
                                : ProjectInvitationStatus.Pending,
                        element.TryGetProperty("respondedAt", out var respondedAtElement) && respondedAtElement.ValueKind != JsonValueKind.Null
                            ? respondedAtElement.GetDateTimeOffset()
                            : (DateTimeOffset?)null))
                    .ToArray()
                : []);
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

        writer.WritePropertyName("subtasks");
        writer.WriteStartArray();

        foreach (var subtask in task.Subtasks)
        {
            WriteTaskSubtask(writer, subtask);
        }

        writer.WriteEndArray();

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

        writer.WritePropertyName("codeTraceability");
        writer.WriteStartObject();
        WriteTaskCodeTraceabilityItems(writer, "api", task.CodeTraceability.Api);
        WriteTaskCodeTraceabilityItems(writer, "frontendPages", task.CodeTraceability.FrontendPages);
        WriteTaskCodeTraceabilityItems(writer, "frontendComponents", task.CodeTraceability.FrontendComponents);
        writer.WriteEndObject();

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
        var subtasks = root.TryGetProperty("subtasks", out var subtasksElement) && subtasksElement.ValueKind != JsonValueKind.Null
            ? subtasksElement
                .EnumerateArray()
                .Select(ReadTaskSubtask)
                .ToArray()
            : [];
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
        var codeTraceability = root.TryGetProperty("codeTraceability", out var codeTraceabilityElement) && codeTraceabilityElement.ValueKind != JsonValueKind.Null
            ? new TaskCodeTraceability(
                ReadTaskCodeTraceabilityItems(codeTraceabilityElement, "api"),
                ReadTaskCodeTraceabilityItems(codeTraceabilityElement, "frontendPages"),
                ReadTaskCodeTraceabilityItems(codeTraceabilityElement, "frontendComponents"))
            : TaskCodeTraceability.Empty;

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
            subtasks,
            reminders,
            codeTraceability,
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

    private static TaskCodeTraceabilityItem[] ReadTaskCodeTraceabilityItems(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var itemsElement) || itemsElement.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return itemsElement
            .EnumerateArray()
            .Select(item => new TaskCodeTraceabilityItem(
                GetRequiredString(item, "changeType"),
                GetRequiredString(item, "target")))
            .ToArray();
    }

    private static ProjectSubtaskTemplate ReadProjectSubtaskTemplate(JsonElement element)
    {
        return new ProjectSubtaskTemplate(
            element.GetProperty("id").GetGuid(),
            GetRequiredString(element, "title"),
            element.TryGetProperty("order", out var orderElement) ? orderElement.GetInt32() : 0);
    }

    private static TaskSubtask ReadTaskSubtask(JsonElement element)
    {
        return new TaskSubtask(
            element.GetProperty("id").GetGuid(),
            GetRequiredString(element, "title"),
            element.TryGetProperty("isChecked", out var isCheckedElement) && isCheckedElement.GetBoolean(),
            element.TryGetProperty("order", out var orderElement) ? orderElement.GetInt32() : 0);
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

    private static void WriteProjectSubtaskTemplate(Utf8JsonWriter writer, ProjectSubtaskTemplate template)
    {
        writer.WriteStartObject();
        writer.WriteString("id", template.Id);
        writer.WriteString("title", template.Title);
        writer.WriteNumber("order", template.Order);
        writer.WriteEndObject();
    }

    private static void WriteTaskSubtask(Utf8JsonWriter writer, TaskSubtask subtask)
    {
        writer.WriteStartObject();
        writer.WriteString("id", subtask.Id);
        writer.WriteString("title", subtask.Title);
        writer.WriteBoolean("isChecked", subtask.IsChecked);
        writer.WriteNumber("order", subtask.Order);
        writer.WriteEndObject();
    }

    private static void WriteTaskCodeTraceabilityItems(Utf8JsonWriter writer, string propertyName, IReadOnlyList<TaskCodeTraceabilityItem> items)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();

        foreach (var item in items)
        {
            writer.WriteStartObject();
            writer.WriteString("changeType", item.ChangeType);
            writer.WriteString("target", item.Target);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
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