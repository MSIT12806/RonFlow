using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;
using RonFlow.Application;
using RonFlow.Domain;
using Task = System.Threading.Tasks.Task;

namespace RonFlow.Api.Tests;

public sealed class ReportingProjectionApiIntegrationTests : ApiIntegrationTestBase
{
    [Test]
    public async Task GetWorkflowThroughputReport_AfterTaskCreatedAndCompleted_ReturnsDailyCounters()
    {
        var project = await CreateProjectAsync("Reporting Project");
        var task = await CreateTaskAsync(project.Id, "Build reporting query");

        var moveToActiveResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/state",
            new ChangeTaskStateRequest("active"));
        Assert.That(moveToActiveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var moveToDoneResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/state",
            new ChangeTaskStateRequest("done"));
        Assert.That(moveToDoneResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        GetRequiredService<ProcessWorkflowThroughputProjectionService>().ProcessPending();

        var response = await Client.GetAsync($"/api/projects/{project.Id}/reports/workflow-throughput?bucket=day");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<WorkflowThroughputReportResponse>();
        Assert.That(report, Is.Not.Null);
        Assert.That(report!.ProjectId, Is.EqualTo(project.Id));
        Assert.That(report.BucketType, Is.EqualTo("day"));
        Assert.That(report.Buckets, Has.Count.EqualTo(1));

        var bucket = report.Buckets.Single();
        Assert.That(bucket.CreatedCount, Is.EqualTo(1));
        Assert.That(bucket.MovedToActiveCount, Is.EqualTo(1));
        Assert.That(bucket.MovedToReviewCount, Is.EqualTo(0));
        Assert.That(bucket.CompletedCount, Is.EqualTo(1));
        Assert.That(bucket.ReopenedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetWorkflowThroughputReport_WhenTaskIsReopened_IncrementsReopenedCounter()
    {
        var project = await CreateProjectAsync("Reporting Reopen Project");
        var task = await CreateTaskAsync(project.Id, "Measure reopen flow");

        var moveToActiveResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/state",
            new ChangeTaskStateRequest("active"));
        Assert.That(moveToActiveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var moveToDoneResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/state",
            new ChangeTaskStateRequest("done"));
        Assert.That(moveToDoneResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var reopenResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/state",
            new ChangeTaskStateRequest("review"));
        Assert.That(reopenResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        GetRequiredService<ProcessWorkflowThroughputProjectionService>().ProcessPending();

        var response = await Client.GetAsync($"/api/projects/{project.Id}/reports/workflow-throughput?bucket=day");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<WorkflowThroughputReportResponse>();
        Assert.That(report, Is.Not.Null);
        Assert.That(report!.Buckets, Has.Count.EqualTo(1));

        var bucket = report.Buckets.Single();
        Assert.That(bucket.CreatedCount, Is.EqualTo(1));
        Assert.That(bucket.MovedToActiveCount, Is.EqualTo(1));
        Assert.That(bucket.MovedToReviewCount, Is.EqualTo(1));
        Assert.That(bucket.CompletedCount, Is.EqualTo(1));
        Assert.That(bucket.ReopenedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetWorkflowThroughputReport_WhenBucketIsWeek_AggregatesDailyEventsIntoOneWeekBucket()
    {
        var project = await CreateProjectAsync("Weekly Reporting Project");
        var firstTask = await CreateTaskAsync(project.Id, "First throughput task");
        var secondTask = await CreateTaskAsync(project.Id, "Second throughput task");

        var firstTaskMoveResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{firstTask.Id}/state",
            new ChangeTaskStateRequest("active"));
        Assert.That(firstTaskMoveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var secondTaskMoveResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{secondTask.Id}/state",
            new ChangeTaskStateRequest("review"));
        Assert.That(secondTaskMoveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        GetRequiredService<ProcessWorkflowThroughputProjectionService>().ProcessPending();

        var response = await Client.GetAsync($"/api/projects/{project.Id}/reports/workflow-throughput?bucket=week");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<WorkflowThroughputReportResponse>();
        Assert.That(report, Is.Not.Null);
        Assert.That(report!.BucketType, Is.EqualTo("week"));
        Assert.That(report.Buckets, Has.Count.EqualTo(1));

        var bucket = report.Buckets.Single();
        Assert.That(bucket.CreatedCount, Is.EqualTo(2));
        Assert.That(bucket.MovedToActiveCount, Is.EqualTo(1));
        Assert.That(bucket.MovedToReviewCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetTaskAgingReport_WhenThresholdIsZero_ReturnsOnlyOpenTasks()
    {
        var project = await CreateProjectAsync("Task Aging Project");
        var openTask = await CreateTaskAsync(project.Id, "Investigate stuck task");
        var completedTask = await CreateTaskAsync(project.Id, "Close finished task");

        var completeResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{completedTask.Id}/state",
            new ChangeTaskStateRequest("done"));
        Assert.That(completeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var response = await Client.GetAsync(
            $"/api/projects/{project.Id}/reports/task-aging?todoThresholdDays=0&activeThresholdDays=0&reviewThresholdDays=0");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<TaskAgingReportResponse>();
        Assert.That(report, Is.Not.Null);
        Assert.That(report!.ProjectId, Is.EqualTo(project.Id));
        Assert.That(report.Items.Select(item => item.TaskId), Does.Contain(openTask.Id));
        Assert.That(report.Items.Select(item => item.TaskId), Does.Not.Contain(completedTask.Id));
        Assert.That(report.Thresholds.Select(item => item.StateKey), Does.Contain("todo"));
    }

    [Test]
    public async Task GetTaskAgingReport_AfterTaskMovesState_UsesCurrentStateEntryTime()
    {
        var project = await CreateProjectAsync("Task Aging State Project");
        var task = await CreateTaskAsync(project.Id, "Measure current state age");

        await Task.Delay(50);

        var moveResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}/tasks/{task.Id}/state",
            new ChangeTaskStateRequest("active"));
        Assert.That(moveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var response = await Client.GetAsync(
            $"/api/projects/{project.Id}/reports/task-aging?todoThresholdDays=0&activeThresholdDays=0&reviewThresholdDays=0");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<TaskAgingReportResponse>();
        Assert.That(report, Is.Not.Null);

        var item = report!.Items.Single(entry => entry.TaskId == task.Id);
        Assert.That(item.CurrentState.Key, Is.EqualTo("active"));
        Assert.That(item.EnteredStateAt, Is.GreaterThan(task.CreatedAt));
    }

    [Test]
    public async Task GetCycleTimeReport_WhenCompletedTasksExist_ReturnsLeadAndCycleMetrics()
    {
        var project = await CreateProjectAsync("Cycle Time Project");
        var firstTask = await CreateTaskAsync(project.Id, "Measure lead time one");
        var secondTask = await CreateTaskAsync(project.Id, "Measure lead time two");

        ChangeTaskStateAt(project.Id, firstTask.Id, "active", firstTask.CreatedAt.AddHours(4));
        ChangeTaskStateAt(project.Id, firstTask.Id, "done", firstTask.CreatedAt.AddHours(10));
        ChangeTaskStateAt(project.Id, secondTask.Id, "active", secondTask.CreatedAt.AddHours(6));
        ChangeTaskStateAt(project.Id, secondTask.Id, "done", secondTask.CreatedAt.AddHours(30));

        var completedFrom = DateOnly.FromDateTime(firstTask.CreatedAt.UtcDateTime.AddDays(-1));
        var completedTo = DateOnly.FromDateTime(secondTask.CreatedAt.UtcDateTime.AddDays(3));

        var response = await Client.GetAsync($"/api/projects/{project.Id}/reports/cycle-time?completedFrom={completedFrom:yyyy-MM-dd}&completedTo={completedTo:yyyy-MM-dd}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<CycleTimeReportResponse>();
        Assert.That(report, Is.Not.Null);
        Assert.That(report!.LeadTime.SampleCount, Is.EqualTo(2));
        Assert.That(report.LeadTime.AverageHours, Is.EqualTo(20).Within(0.01));
        Assert.That(report.LeadTime.MedianHours, Is.EqualTo(20).Within(0.01));
        Assert.That(report.LeadTime.P90Hours, Is.EqualTo(30).Within(0.01));
        Assert.That(report.CycleTime.SampleCount, Is.EqualTo(2));
        Assert.That(report.CycleTime.AverageHours, Is.EqualTo(15).Within(0.01));
        Assert.That(report.CycleTime.MedianHours, Is.EqualTo(15).Within(0.01));
        Assert.That(report.CycleTime.P90Hours, Is.EqualTo(24).Within(0.01));
    }

    [Test]
    public async Task GetCycleTimeReport_WhenNoTaskEnteredActive_ReturnsInsufficientCycleSample()
    {
        var project = await CreateProjectAsync("Cycle Time Missing Active Project");
        var task = await CreateTaskAsync(project.Id, "Done without active");

        ChangeTaskStateAt(project.Id, task.Id, "done", task.CreatedAt.AddHours(8));

        var completedFrom = DateOnly.FromDateTime(task.CreatedAt.UtcDateTime.AddDays(-1));
        var completedTo = DateOnly.FromDateTime(task.CreatedAt.UtcDateTime.AddDays(2));

        var response = await Client.GetAsync($"/api/projects/{project.Id}/reports/cycle-time?completedFrom={completedFrom:yyyy-MM-dd}&completedTo={completedTo:yyyy-MM-dd}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<CycleTimeReportResponse>();
        Assert.That(report, Is.Not.Null);
        Assert.That(report!.LeadTime.SampleCount, Is.EqualTo(1));
        Assert.That(report.LeadTime.AverageHours, Is.EqualTo(8).Within(0.01));
        Assert.That(report.CycleTime.SampleCount, Is.EqualTo(0));
        Assert.That(report.CycleTime.AverageHours, Is.Null);
        Assert.That(report.CycleTime.MedianHours, Is.Null);
        Assert.That(report.CycleTime.P90Hours, Is.Null);
    }

    private void ChangeTaskStateAt(Guid projectId, Guid taskId, string stateKey, DateTimeOffset changedAt)
    {
        var project = GetRequiredService<IProjectRepository>().Get(projectId);
        var taskRepository = GetRequiredService<ITaskRepository>();
        var task = taskRepository.Get(taskId);

        Assert.That(project, Is.Not.Null);
        Assert.That(task, Is.Not.Null);

        var targetState = project!.WorkflowStates.Single(state => string.Equals(state.Key, stateKey, StringComparison.OrdinalIgnoreCase));
        var result = task!.ChangeState(TaskMutationAuthorization.Granted(TaskMutationKind.ChangeWorkflowState), targetState, changedAt);
        Assert.That(result.Changed, Is.True);

        taskRepository.Update(task);
    }
}