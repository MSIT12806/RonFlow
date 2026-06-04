using System.Net;
using System.Net.Http.Json;
using RonFlow.Api.Contracts;
using RonFlow.Application;

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
}