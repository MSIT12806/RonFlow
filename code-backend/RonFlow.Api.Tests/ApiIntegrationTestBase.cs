using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Tests;

public abstract class ApiIntegrationTestBase
{
    protected sealed record TestUser(Guid UserId, string UserName, string Email)
    {
        public static TestUser OwnerA { get; } = new(new Guid("11111111-1111-1111-1111-111111111111"), "owner-a", "owner-a@example.test");

        public static TestUser OwnerB { get; } = new(new Guid("22222222-2222-2222-2222-222222222222"), "owner-b", "owner-b@example.test");
    }

    private RonFlowApiFactory factory = null!;

    protected HttpClient Client { get; private set; } = null!;

    [SetUp]
    public void SetUpBase()
    {
        factory = new RonFlowApiFactory();
        Client = CreateAuthenticatedClient(TestUser.OwnerA);
    }

    [TearDown]
    public void TearDownBase()
    {
        Client.Dispose();
        factory.Dispose();
    }

    protected HttpClient CreateAnonymousClient()
    {
        return factory.CreateClient();
    }

    protected HttpClient CreateAuthenticatedClient(TestUser user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(user));
        return client;
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

    protected async Task<ProjectResponse> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(name));
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.That(project, Is.Not.Null);

        return project!;
    }

    protected async Task<TaskDetailResponse> CreateTaskAsync(HttpClient client, Guid projectId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new CreateTaskRequest(title));
        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<TaskDetailResponse>();
        Assert.That(task, Is.Not.Null);

        return task!;
    }

    protected static async Task AssertAccessDeniedAsync(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Access Denied").IgnoreCase);
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

    protected T GetRequiredService<T>() where T : notnull
    {
        return factory.Services.GetRequiredService<T>();
    }

    private static string CreateAccessToken(TestUser user)
    {
        const string issuer = "RonAuth";
        const string audience = "RonFlow.Client";
        const string signingKey = "RonAuth-Development-Signing-Key-For-Local-Only-1234567890";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}