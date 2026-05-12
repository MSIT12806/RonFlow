using System.Text.Json;
using RonFlow.Domain;
using DomainTask = RonFlow.Domain.Task;

namespace RonFlow.Infrastructure;

internal static class CoreFlowJsonSerializer
{
    public static string Serialize(Project project)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("id", project.Id);
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
        writer.WritePropertyName("currentState");
        WriteWorkflowState(writer, task.CurrentState);
        writer.WriteString("createdAt", task.CreatedAt);

        if (task.CompletedAt is null)
        {
            writer.WriteNull("completedAt");
        }
        else
        {
            writer.WriteString("completedAt", task.CompletedAt.Value);
        }

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

        return DomainTask.Rehydrate(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("projectId").GetGuid(),
            GetRequiredString(root, "title"),
            ReadWorkflowState(root.GetProperty("currentState")),
            root.GetProperty("createdAt").GetDateTimeOffset(),
            completedAt,
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
        return new WorkflowState(
            GetRequiredString(element, "key"),
            GetRequiredString(element, "label"),
            element.GetProperty("isInitialState").GetBoolean());
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