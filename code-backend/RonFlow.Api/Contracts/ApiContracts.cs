using RonFlow.Application;
using RonFlow.Domain;
using System.Text.Json;

namespace RonFlow.Api.Contracts;

public sealed record CreateProjectRequest(string? Name);

public sealed record AiActiveScopeRequest(Guid? ProjectId);

public sealed record AiApplyRequest(
    string? Operation,
    string? TargetType,
    string? TargetId,
    Dictionary<string, JsonElement>? RequiredFields,
    Dictionary<string, JsonElement>? OptionalFields,
    string? Note);

public sealed record CreateProjectInvitationRequest(string? Invitee);

public sealed record CreateTaskRequest(string? Title);

public sealed record ChangeTaskStateRequest(string? StateKey);

public sealed record CreateTaskReminderRequest(string? ReminderDateTime, string? Description);

public sealed record PushSubscriptionKeysRequest(string? P256dh, string? Auth);

public sealed record RegisterPushSubscriptionRequest(string? Endpoint, PushSubscriptionKeysRequest? Keys);

public sealed class TaskCodeTraceabilityItemRequest
{
    public string? ChangeType { get; init; }

    public string? Target { get; init; }
}

public sealed class TaskCodeTraceabilityRequest
{
    public List<TaskCodeTraceabilityItemRequest>? Api { get; init; }

    public List<TaskCodeTraceabilityItemRequest>? FrontendPages { get; init; }

    public List<TaskCodeTraceabilityItemRequest>? FrontendComponents { get; init; }
}

public sealed class UpdateTaskRequest
{
    public UpdateTaskRequest()
    {
    }

    public UpdateTaskRequest(
        string? title,
        string? description,
        DateOnly? dueDate,
        JsonElement? codeTraceability = null)
    {
        Title = title;
        Description = description;
        DueDate = dueDate;
        CodeTraceability = codeTraceability;
    }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public DateOnly? DueDate { get; set; }

    public JsonElement? CodeTraceability { get; set; }
}

public sealed record ReplaceProjectSubtaskTemplatesRequest(IReadOnlyList<ProjectSubtaskTemplateItemRequest>? Items);

public sealed record ProjectSubtaskTemplateItemRequest(Guid? Id, string? Title, int? Order);

public sealed record ReplaceTaskSubtasksRequest(IReadOnlyList<TaskSubtaskItemRequest>? Items);

public sealed record TaskSubtaskItemRequest(Guid? Id, string? Title, bool IsChecked, int? Order);

public sealed record ReorderTaskRequest(Guid? TargetTaskId);

public sealed record PushNotificationPublicKeyResponse(string PublicKey);

public sealed record DeploymentComponentResponse(
    string Application,
    string Version,
    string InformationalVersion,
    DateTimeOffset UpdatedAtUtc,
    string? SourceRevision);

public sealed record DeploymentSummaryResponse(
    string Environment,
    DeploymentComponentResponse Frontend,
    DeploymentComponentResponse RonFlowApi,
    DeploymentComponentResponse RonAuthApi,
    bool IsSameDeployment);

public sealed record ProjectListResponse(IReadOnlyList<ProjectListItemResponse> Items);

public sealed record ProjectMemberListResponse(
    IReadOnlyList<ProjectMemberResponse> Items,
    IReadOnlyList<ProjectOnlineUserResponse> OnlineUsers)
{
    public static ProjectMemberListResponse FromView(ProjectMemberListView view)
    {
        return new(
            view.Items.Select(ProjectMemberResponse.FromView).ToArray(),
            view.OnlineUsers.Select(ProjectOnlineUserResponse.FromView).ToArray());
    }
}

public sealed record ProjectMemberResponse(string UserName, string Role)
{
    public static ProjectMemberResponse FromView(ProjectMemberView view)
    {
        return new(view.UserName, view.Role);
    }
}

public sealed record ProjectOnlineUserResponse(string UserName)
{
    public static ProjectOnlineUserResponse FromView(ProjectOnlineUserView view)
    {
        return new(view.UserName);
    }
}

public sealed record ProjectInvitationListResponse(IReadOnlyList<ProjectInvitationResponse> Items)
{
    public static ProjectInvitationListResponse FromView(ProjectInvitationListView view)
    {
        return new(view.Items.Select(ProjectInvitationResponse.FromView).ToArray());
    }
}

public sealed record ProjectInvitationResponse(Guid Id, string Invitee, string Status)
{
    public static ProjectInvitationResponse FromView(ProjectInvitationView view)
    {
        return new(view.Id, view.Invitee, view.Status);
    }
}

public sealed record InvitationInboxResponse(IReadOnlyList<InvitationInboxItemResponse> Items)
{
    public static InvitationInboxResponse FromView(InvitationInboxView view)
    {
        return new(view.Items.Select(InvitationInboxItemResponse.FromView).ToArray());
    }
}

public sealed record InvitationInboxItemResponse(Guid Id, Guid ProjectId, string ProjectName, string InviterName)
{
    public static InvitationInboxItemResponse FromView(InvitationInboxItemView view)
    {
        return new(view.Id, view.ProjectId, view.ProjectName, view.InviterName);
    }
}

public sealed record ProjectListItemResponse(Guid Id, string Name, DateTimeOffset UpdatedAt, string Role)
{
    public static ProjectListItemResponse FromView(ProjectListItemView view)
    {
        return new(view.Id, view.Name, view.UpdatedAt, view.Role);
    }
}

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowStateResponse> WorkflowStates)
{
    public static ProjectResponse FromOutput(CreateProjectOutput output)
    {
        return new(
            output.Id,
            output.Name,
            output.UpdatedAt,
            output.WorkflowStates.Select(WorkflowStateResponse.FromOutput).ToArray());
    }

    public static ProjectResponse FromView(ProjectView view)
    {
        return new(
            view.Id,
            view.Name,
            view.UpdatedAt,
            view.WorkflowStates.Select(WorkflowStateResponse.FromView).ToArray());
    }
}

public sealed record ProjectBoardResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<BoardColumnResponse> Columns)
{
    public static ProjectBoardResponse FromView(ProjectBoardView view)
    {
        var columns = view.Columns.Select(BoardColumnResponse.FromView).ToArray();

        return new(view.ProjectId, view.ProjectName, columns);
    }
}

public sealed record BoardColumnResponse(
    string StateKey,
    string Label,
    bool IsInitialState,
    bool IsCompletedState,
    string EmptyStateMessage,
    IReadOnlyList<BoardTaskCardResponse> Tasks)
{
    public static BoardColumnResponse FromView(BoardColumnView view)
    {
        return new(
            view.StateKey,
            view.Label,
            view.IsInitialState,
            view.IsCompletedState,
            view.EmptyStateMessage,
            view.Tasks.Select(BoardTaskCardResponse.FromView).ToArray());
    }
}

public sealed record BoardTaskCardResponse(Guid Id, string Title)
{
    public static BoardTaskCardResponse FromView(BoardTaskCardView view)
    {
        return new(view.Id, view.Title);
    }
}

public sealed record ProjectCodeTraceabilityResponse(IReadOnlyList<ProjectCodeTraceabilityItemResponse> Items)
{
    public static ProjectCodeTraceabilityResponse FromView(ProjectCodeTraceabilityView view)
    {
        return new(view.Items.Select(ProjectCodeTraceabilityItemResponse.FromView).ToArray());
    }
}

public sealed record ProjectCodeTraceabilityItemResponse(
    Guid TaskId,
    string TaskTitle,
    string Category,
    string ChangeType,
    string Target)
{
    public static ProjectCodeTraceabilityItemResponse FromView(ProjectCodeTraceabilityItemView view)
    {
        return new(view.TaskId, view.TaskTitle, view.Category, view.ChangeType, view.Target);
    }
}

public sealed record WorkflowStateResponse(string Key, string Label, bool IsInitialState, bool IsCompletedState)
{
    public static WorkflowStateResponse FromOutput(CreatedWorkflowStateOutput output)
    {
        return new(output.Key, output.Label, output.IsInitialState, output.IsCompletedState);
    }

    public static WorkflowStateResponse FromView(WorkflowStateView view)
    {
        return new(view.Key, view.Label, view.IsInitialState, view.IsCompletedState);
    }
}

public sealed record ProjectSubtaskTemplateListResponse(IReadOnlyList<ProjectSubtaskTemplateResponse> Items)
{
    public static ProjectSubtaskTemplateListResponse FromView(ProjectSubtaskTemplateListView view)
    {
        return new(view.Items.Select(ProjectSubtaskTemplateResponse.FromView).ToArray());
    }

    public static ProjectSubtaskTemplateListResponse FromOutput(IReadOnlyList<ProjectSubtaskTemplateOutput> outputs)
    {
        return new(outputs.Select(ProjectSubtaskTemplateResponse.FromOutput).ToArray());
    }
}

public sealed record ProjectSubtaskTemplateResponse(Guid Id, string Title, int Order)
{
    public static ProjectSubtaskTemplateResponse FromView(ProjectSubtaskTemplateView view)
    {
        return new(view.Id, view.Title, view.Order);
    }

    public static ProjectSubtaskTemplateResponse FromOutput(ProjectSubtaskTemplateOutput output)
    {
        return new(output.Id, output.Title, output.Order);
    }
}

public sealed record TaskSubtaskResponse(Guid Id, string Title, bool IsChecked, int Order)
{
    public static TaskSubtaskResponse FromView(TaskSubtaskView view)
    {
        return new(view.Id, view.Title, view.IsChecked, view.Order);
    }

    public static TaskSubtaskResponse FromOutput(TaskSubtaskOutput output)
    {
        return new(output.Id, output.Title, output.IsChecked, output.Order);
    }
}

public sealed record TaskCodeTraceabilityItemResponse(string ChangeType, string Target)
{
    public static TaskCodeTraceabilityItemResponse FromView(TaskCodeTraceabilityItemView view)
    {
        return new(view.ChangeType, view.Target);
    }

    public static TaskCodeTraceabilityItemResponse FromOutput(TaskCodeTraceabilityItemOutput output)
    {
        return new(output.ChangeType, output.Target);
    }
}

public sealed record TaskCodeTraceabilityResponse(
    IReadOnlyList<TaskCodeTraceabilityItemResponse> Api,
    IReadOnlyList<TaskCodeTraceabilityItemResponse> FrontendPages,
    IReadOnlyList<TaskCodeTraceabilityItemResponse> FrontendComponents)
{
    public static TaskCodeTraceabilityResponse FromView(TaskCodeTraceabilityView view)
    {
        return new(
            view.Api.Select(TaskCodeTraceabilityItemResponse.FromView).ToArray(),
            view.FrontendPages.Select(TaskCodeTraceabilityItemResponse.FromView).ToArray(),
            view.FrontendComponents.Select(TaskCodeTraceabilityItemResponse.FromView).ToArray());
    }

    public static TaskCodeTraceabilityResponse FromOutput(TaskCodeTraceabilityOutput output)
    {
        return new(
            output.Api.Select(TaskCodeTraceabilityItemResponse.FromOutput).ToArray(),
            output.FrontendPages.Select(TaskCodeTraceabilityItemResponse.FromOutput).ToArray(),
            output.FrontendComponents.Select(TaskCodeTraceabilityItemResponse.FromOutput).ToArray());
    }
}

public sealed record TaskDetailResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    WorkflowStateResponse CurrentState,
    string LifecycleState,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TaskSubtaskResponse> Subtasks,
    TaskCodeTraceabilityResponse CodeTraceability,
    IReadOnlyList<TaskReminderResponse> Reminders,
    IReadOnlyList<ActivityTimelineItemResponse> ActivityTimeline,
    bool CanEnterEdit)
{
    public static TaskDetailResponse FromOutput(CreateTaskOutput output, bool canEnterEdit = true)
    {
        return new(
            output.Id,
            output.ProjectId,
            output.Title,
            output.Description,
            WorkflowStateResponse.FromOutput(output.CurrentState),
            ToTaskLifecycleStateResponse(output.LifecycleState),
            output.DueDate,
            output.CreatedAt,
            output.CompletedAt,
            output.Subtasks.Select(TaskSubtaskResponse.FromOutput).ToArray(),
            TaskCodeTraceabilityResponse.FromOutput(output.CodeTraceability),
            output.Reminders.Select(TaskReminderResponse.FromOutput).ToArray(),
            output.ActivityTimeline.Select(ActivityTimelineItemResponse.FromOutput).ToArray(),
            canEnterEdit);
    }

    public static TaskDetailResponse FromView(TaskDetailView view, bool canEnterEdit = true)
    {
        return new(
            view.Id,
            view.ProjectId,
            view.Title,
            view.Description,
            WorkflowStateResponse.FromView(view.CurrentState),
            ToTaskLifecycleStateResponse(view.LifecycleState),
            view.DueDate,
            view.CreatedAt,
            view.CompletedAt,
            view.Subtasks.Select(TaskSubtaskResponse.FromView).ToArray(),
            TaskCodeTraceabilityResponse.FromView(view.CodeTraceability),
            view.Reminders.Select(TaskReminderResponse.FromView).ToArray(),
            view.ActivityTimeline.Select(ActivityTimelineItemResponse.FromView).ToArray(),
            canEnterEdit);
    }

    private static string ToTaskLifecycleStateResponse(TaskLifecycleState lifecycleState)
    {
        return lifecycleState switch
        {
            TaskLifecycleState.ActiveRecord => "activeRecord",
            TaskLifecycleState.Archived => "archived",
            TaskLifecycleState.Trashed => "trashed",
            _ => "activeRecord",
        };
    }
}

public sealed record TaskReminderResponse(Guid Id, string ReminderDateTime, string Description)
{
    public static TaskReminderResponse FromOutput(CreatedTaskReminderOutput output)
    {
        return new(output.Id, output.ReminderDateTime, output.Description);
    }

    public static TaskReminderResponse FromView(TaskReminderView view)
    {
        return new(view.Id, view.ReminderDateTime, view.Description);
    }
}

public sealed record ActivityTimelineItemResponse(string Type, string Message, DateTimeOffset OccurredAt)
{
    public static ActivityTimelineItemResponse FromOutput(CreatedActivityTimelineItemOutput output)
    {
        return new(output.Type, output.Message, output.OccurredAt);
    }

    public static ActivityTimelineItemResponse FromView(ActivityTimelineItemView view)
    {
        return new(view.Type, view.Message, view.OccurredAt);
    }
}

public sealed record LifecycleTaskListResponse(IReadOnlyList<LifecycleTaskListItemResponse> Items)
{
    public static LifecycleTaskListResponse FromView(LifecycleTaskListView view)
    {
        return new(view.Items.Select(LifecycleTaskListItemResponse.FromView).ToArray());
    }
}

public sealed record LifecycleTaskListItemResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    WorkflowStateResponse OriginalState,
    DateTimeOffset ChangedAt)
{
    public static LifecycleTaskListItemResponse FromView(LifecycleTaskListItemView view)
    {
        return new(
            view.Id,
            view.ProjectId,
            view.ProjectName,
            view.Title,
            WorkflowStateResponse.FromView(view.OriginalState),
            view.ChangedAt);
    }
}

public sealed record WorkflowThroughputBucketResponse(
    DateOnly BucketStart,
    int CreatedCount,
    int MovedToActiveCount,
    int MovedToReviewCount,
    int CompletedCount,
    int ReopenedCount)
{
    public static WorkflowThroughputBucketResponse FromView(WorkflowThroughputBucketView view)
    {
        return new(
            view.BucketStart,
            view.CreatedCount,
            view.MovedToActiveCount,
            view.MovedToReviewCount,
            view.CompletedCount,
            view.ReopenedCount);
    }
}

public sealed record WorkflowThroughputReportResponse(
    Guid ProjectId,
    string BucketType,
    DateTimeOffset? LastUpdatedAt,
    IReadOnlyList<WorkflowThroughputBucketResponse> Buckets)
{
    public static WorkflowThroughputReportResponse FromView(WorkflowThroughputReportView view)
    {
        return new(
            view.ProjectId,
            view.BucketType,
            view.LastUpdatedAt,
            view.Buckets.Select(WorkflowThroughputBucketResponse.FromView).ToArray());
    }
}

public sealed record TaskAgingStateThresholdResponse(
    string StateKey,
    string StateLabel,
    int ThresholdDays)
{
    public static TaskAgingStateThresholdResponse FromView(TaskAgingStateThresholdView view)
    {
        return new(view.StateKey, view.StateLabel, view.ThresholdDays);
    }
}

public sealed record TaskAgingTaskItemResponse(
    Guid TaskId,
    string Title,
    WorkflowStateResponse CurrentState,
    DateTimeOffset EnteredStateAt,
    int AgingDays)
{
    public static TaskAgingTaskItemResponse FromView(TaskAgingTaskItemView view)
    {
        return new(
            view.TaskId,
            view.Title,
            WorkflowStateResponse.FromView(view.CurrentState),
            view.EnteredStateAt,
            view.AgingDays);
    }
}

public sealed record TaskAgingReportResponse(
    Guid ProjectId,
    DateTimeOffset LastUpdatedAt,
    IReadOnlyList<TaskAgingStateThresholdResponse> Thresholds,
    IReadOnlyList<TaskAgingTaskItemResponse> Items)
{
    public static TaskAgingReportResponse FromView(TaskAgingReportView view)
    {
        return new(
            view.ProjectId,
            view.LastUpdatedAt,
            view.Thresholds.Select(TaskAgingStateThresholdResponse.FromView).ToArray(),
            view.Items.Select(TaskAgingTaskItemResponse.FromView).ToArray());
    }
}

public sealed record CycleTimeMetricSummaryResponse(
    int SampleCount,
    double? AverageHours,
    double? MedianHours,
    double? P90Hours)
{
    public static CycleTimeMetricSummaryResponse FromView(CycleTimeMetricSummaryView view)
    {
        return new(view.SampleCount, view.AverageHours, view.MedianHours, view.P90Hours);
    }
}

public sealed record CycleTimeReportResponse(
    Guid ProjectId,
    DateOnly CompletedFrom,
    DateOnly CompletedTo,
    DateTimeOffset LastUpdatedAt,
    CycleTimeMetricSummaryResponse LeadTime,
    CycleTimeMetricSummaryResponse CycleTime)
{
    public static CycleTimeReportResponse FromView(CycleTimeReportView view)
    {
        return new(
            view.ProjectId,
            view.CompletedFrom,
            view.CompletedTo,
            view.LastUpdatedAt,
            CycleTimeMetricSummaryResponse.FromView(view.LeadTime),
            CycleTimeMetricSummaryResponse.FromView(view.CycleTime));
    }
}