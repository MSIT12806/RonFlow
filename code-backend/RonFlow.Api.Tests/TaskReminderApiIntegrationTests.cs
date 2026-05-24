using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public sealed class TaskReminderApiIntegrationTests : ApiIntegrationTestBase
{
    public sealed record TaskReminderResponse(Guid Id, string ReminderDateTime, string Description);

    public sealed record ReminderTaskDetailResponse(
        Guid Id,
        Guid ProjectId,
        string Title,
        string Description,
        WorkflowStateResponse CurrentState,
        DateOnly? DueDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        IReadOnlyList<TaskReminderResponse> Reminders,
        IReadOnlyList<ActivityTimelineItemResponse> ActivityTimeline);

    private async Task AcquireContentEditLockAsync(Guid projectId, Guid taskId)
    {
        var response = await Client.PostAsync($"/api/projects/{projectId}/tasks/{taskId}/content-edit-lock", content: null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CreateReminder_WithBlankReminderDateTime_ReturnsValidationError()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var task = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders",
            new { reminderDateTime = "", description = "提醒確認欄位狀態" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var errors = await ReadValidationErrorsAsync(response);

        Assert.That(errors, Does.ContainKey("reminderDateTime"));
        Assert.That(errors["reminderDateTime"], Does.Contain("提醒時間為必填欄位"));
    }

    [Test]
    public async Task CreateReminder_WithValidPayload_ReturnsCreatedTaskDetailIncludingReminder()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var task = await CreateTaskAsync(project.Id, "Build Kanban Board");

        await AcquireContentEditLockAsync(project.Id, task.Id);

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders",
            new { reminderDateTime = "2026-05-20T09:00", description = "提醒確認欄位狀態" });

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updatedTask = await createResponse.Content.ReadFromJsonAsync<ReminderTaskDetailResponse>();

        Assert.That(updatedTask, Is.Not.Null);
        Assert.That(updatedTask!.Id, Is.EqualTo(task.Id));
        Assert.That(updatedTask.Reminders, Has.Count.EqualTo(1));
        Assert.That(updatedTask.Reminders[0].ReminderDateTime, Is.EqualTo("2026-05-20T09:00"));
        Assert.That(updatedTask.Reminders[0].Description, Is.EqualTo("提醒確認欄位狀態"));

        var detailResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{task.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReminderTaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Reminders, Has.Count.EqualTo(1));
        Assert.That(detail.Reminders[0].ReminderDateTime, Is.EqualTo("2026-05-20T09:00"));
        Assert.That(detail.Reminders[0].Description, Is.EqualTo("提醒確認欄位狀態"));
    }

    [Test]
    public async Task CreateReminder_WhenCallerHasNotAcquiredContentEditLock_ReturnsConflict()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var task = await CreateTaskAsync(project.Id, "Build Kanban Board");

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders",
            new { reminderDateTime = "2026-05-20T09:00", description = "提醒確認欄位狀態" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task CreateReminder_WhenOldRonFlowSessionIsInvalidated_ReturnsUnauthorized()
    {
        using var firstSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-reminder-session-1");
        using var secondSessionClient = CreateSessionAuthenticatedClient(TestUser.OwnerA, "owner-a-reminder-session-2");

        await EnsureKnownUserAsync(firstSessionClient);
        await EnsureKnownUserAsync(secondSessionClient);

        var project = await CreateProjectAsync(firstSessionClient, "RonFlow Project");
        var task = await CreateTaskAsync(firstSessionClient, project.Id, "Build Kanban Board");

        await ActivateSessionAsync(firstSessionClient);
        await ActivateSessionAsync(secondSessionClient);

        var response = await firstSessionClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders",
            new { reminderDateTime = "2026-05-20T09:00", description = "舊 session 不得新增提醒" });

        await AssertSessionInvalidatedAsync(response);
    }

    [Test]
    public async Task CreateReminder_ForSameTask_AllowsMultipleReminders()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var task = await CreateTaskAsync(project.Id, "Build Kanban Board");

        await AcquireContentEditLockAsync(project.Id, task.Id);

        var firstResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders",
            new { reminderDateTime = "2026-05-20T09:00", description = "提醒確認欄位狀態" });

        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var secondResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders",
            new { reminderDateTime = "2026-05-21T15:00", description = "提醒追蹤審查結果" });

        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var detailResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{task.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReminderTaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Reminders, Has.Count.EqualTo(2));
        Assert.That(detail.Reminders.Select(reminder => reminder.Description), Is.EqualTo(new[]
        {
            "提醒確認欄位狀態",
            "提醒追蹤審查結果",
        }));
    }

    [Test]
    public async Task CreateReminder_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var project = await CreateProjectAsync("RonFlow Project");

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{Guid.NewGuid()}/reminders",
            new { reminderDateTime = "2026-05-20T09:00", description = "提醒確認欄位狀態" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(problem.Detail, Is.EqualTo("找不到指定的任務，無法新增提醒。"));
    }

    [Test]
    public async Task DeleteReminder_RemovesReminderFromTaskDetail()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var task = await CreateTaskAsync(project.Id, "Build Kanban Board");

        await AcquireContentEditLockAsync(project.Id, task.Id);

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders",
            new { reminderDateTime = "2026-05-20T09:00", description = "提醒確認欄位狀態" });

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var createdTask = await createResponse.Content.ReadFromJsonAsync<ReminderTaskDetailResponse>();

        Assert.That(createdTask, Is.Not.Null);

        var reminderId = createdTask!.Reminders.Single().Id;

        var deleteResponse = await Client.DeleteAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders/{reminderId}");

        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updatedTask = await deleteResponse.Content.ReadFromJsonAsync<ReminderTaskDetailResponse>();

        Assert.That(updatedTask, Is.Not.Null);
        Assert.That(updatedTask!.Reminders, Is.Empty);

        var detailResponse = await Client.GetAsync($"/api/projects/{project.Id}/tasks/{task.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReminderTaskDetailResponse>();

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Reminders, Is.Empty);
    }

    [Test]
    public async Task DeleteReminder_WhenReminderDoesNotExist_ReturnsNotFound()
    {
        var project = await CreateProjectAsync("RonFlow Project");
        var task = await CreateTaskAsync(project.Id, "Build Kanban Board");

        await AcquireContentEditLockAsync(project.Id, task.Id);

        var response = await Client.DeleteAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/reminders/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(problem.Detail, Is.EqualTo("找不到指定的提醒，無法刪除。"));
    }
}