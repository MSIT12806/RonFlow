
using System.Net.Mime;
using RonFlow.Api.Contracts;
using RonFlow.Api.Domain;

namespace RonFlow.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddSingleton<RonFlowStore>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet("/api/projects", (RonFlowStore store) =>
        {
            var projects = store.GetProjects()
                .Select(ProjectListItemResponse.FromModel)
                .ToArray();

            return Results.Ok(new ProjectListResponse(projects));
        });

        app.MapPost("/api/projects", (CreateProjectRequest request, RonFlowStore store) =>
        {
            var name = request.Name?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return ValidationResults.ProjectNameRequired();
            }

            var project = store.CreateProject(name);

            return Results.Created(
                $"/api/projects/{project.Id}/board",
                ProjectResponse.FromModel(project));
        })
        .Accepts<CreateProjectRequest>(MediaTypeNames.Application.Json)
        .Produces<ProjectResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/projects/{projectId:guid}/board", (Guid projectId, RonFlowStore store) =>
        {
            var board = store.GetBoard(projectId);
            return board is null ? Results.NotFound() : Results.Ok(ProjectBoardResponse.FromModel(board));
        });

        app.MapPost("/api/projects/{projectId:guid}/tasks", (Guid projectId, CreateTaskRequest request, RonFlowStore store) =>
        {
            var title = request.Title?.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                return ValidationResults.TaskTitleRequired();
            }

            var task = store.CreateTask(projectId, title);
            return task is null
                ? Results.NotFound()
                : Results.Created($"/api/projects/{projectId}/tasks/{task.Id}", TaskDetailResponse.FromModel(task));
        })
        .Accepts<CreateTaskRequest>(MediaTypeNames.Application.Json)
        .Produces<TaskDetailResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/projects/{projectId:guid}/tasks/{taskId:guid}", (Guid projectId, Guid taskId, RonFlowStore store) =>
        {
            var task = store.GetTask(projectId, taskId);
            return task is null ? Results.NotFound() : Results.Ok(TaskDetailResponse.FromModel(task));
        })
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.Run();
    }
}

internal static class ValidationResults
{
    public static IResult ProjectNameRequired()
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["專案名稱為必填欄位"],
        });
    }

    public static IResult TaskTitleRequired()
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["任務標題為必填欄位"],
        });
    }
}
