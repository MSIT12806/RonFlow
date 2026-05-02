using System.Net.Http.Json;
using System.Text.Json;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public abstract class ApiIntegrationTestBase
{
    private RonFlowApiFactory factory = null!;

    protected HttpClient Client { get; private set; } = null!;

    [SetUp]
    public void SetUpBase()
    {
        factory = new RonFlowApiFactory();
        Client = factory.CreateClient();
    }

    [TearDown]
    public void TearDownBase()
    {
        Client.Dispose();
        factory.Dispose();
    }

    protected async Task<ProjectResponse> CreateProjectAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(name));
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.That(project, Is.Not.Null);

        return project!;
    }

    protected async Task<TaskDetailResponse> CreateTaskAsync(Guid projectId, string title)
    {
        var response = await Client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new CreateTaskRequest(title));
        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<TaskDetailResponse>();
        Assert.That(task, Is.Not.Null);

        return task!;
    }

    protected static async Task<IReadOnlyDictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        return document.RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value
                    .EnumerateArray()
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray());
    }
}