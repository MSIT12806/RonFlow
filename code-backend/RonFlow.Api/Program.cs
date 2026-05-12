
using System.Net.Mime;
using RonFlow.Application;
using RonFlow.Api.Contracts;
using RonFlow.Domain;
using RonFlow.Infrastructure;

namespace RonFlow.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        ConfigurePersistence(builder);
        builder.Services.AddSingleton<CreateProjectCommandService>();
        builder.Services.AddSingleton<CreateTaskCommandService>();
        builder.Services.AddSingleton<ChangeTaskStateCommandService>();
        builder.Services.AddSingleton<GetProjectsQueryService>();
        builder.Services.AddSingleton<GetProjectBoardQueryService>();
        builder.Services.AddSingleton<GetTaskDetailQueryService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet("/api/projects", (GetProjectsQueryService queryService) =>
        {
            var projects = queryService.Get().Items
                .Select(ProjectListItemResponse.FromView)
                .ToArray();

            return Results.Ok(new ProjectListResponse(projects));
        });

        app.MapPost("/api/projects", (CreateProjectRequest request, CreateProjectCommandService commandService) =>
        {
            var result = commandService.Create(request.Name);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            var project = result.Project!;

            return Results.Created(
                $"/api/projects/{project.Id}/board",
                ProjectResponse.FromOutput(project));
        })
        .Accepts<CreateProjectRequest>(MediaTypeNames.Application.Json)
        .Produces<ProjectResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/projects/{projectId:guid}/board", (Guid projectId, GetProjectBoardQueryService queryService) =>
        {
            var board = queryService.Get(projectId);
            return board is null ? Results.NotFound() : Results.Ok(ProjectBoardResponse.FromView(board));
        });

        app.MapPost("/api/projects/{projectId:guid}/tasks", (Guid projectId, CreateTaskRequest request, CreateTaskCommandService commandService) =>
        {
            var result = commandService.Create(projectId, request.Title);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            if (result.ProjectNotFound)
            {
                return Results.NotFound();
            }

            var task = result.Task!;

            return Results.Created($"/api/projects/{projectId}/tasks/{task.Id}", TaskDetailResponse.FromOutput(task));
        })
        .Accepts<CreateTaskRequest>(MediaTypeNames.Application.Json)
        .Produces<TaskDetailResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}/state", (
            Guid projectId,
            Guid taskId,
            ChangeTaskStateRequest request,
            ChangeTaskStateCommandService commandService) =>
        {
            var result = commandService.Change(projectId, taskId, request.StateKey ?? string.Empty);

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Accepts<ChangeTaskStateRequest>(MediaTypeNames.Application.Json)
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/projects/{projectId:guid}/tasks/{taskId:guid}", (Guid projectId, Guid taskId, GetTaskDetailQueryService queryService) =>
        {
            var task = queryService.Get(projectId, taskId);
            return task is null ? Results.NotFound() : Results.Ok(TaskDetailResponse.FromView(task));
        })
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.Run();
    }

    private static void ConfigurePersistence(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
            builder.Services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            builder.Services.AddSingleton<ICoreFlowReadStore, InMemoryCoreFlowReadStore>();
            return;
        }

        var configuredDatabasePath = builder.Configuration["Persistence:Sqlite:DatabasePath"];
        var databasePath = string.IsNullOrWhiteSpace(configuredDatabasePath)
            ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "ronflow.db")
            : ResolveDatabasePath(builder.Environment.ContentRootPath, configuredDatabasePath);

        builder.Services.AddSingleton(new SqliteCoreFlowStore(databasePath));
        builder.Services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
        builder.Services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        builder.Services.AddSingleton<ICoreFlowReadStore, SqliteCoreFlowReadStore>();
    }

    private static string ResolveDatabasePath(string contentRootPath, string configuredDatabasePath)
    {
        return Path.IsPathRooted(configuredDatabasePath)
            ? configuredDatabasePath
            : Path.Combine(contentRootPath, configuredDatabasePath);
    }
}

internal static class ValidationResults
{
    public static IResult FromError(ValidationError error)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [error.Field] = [error.Message],
        });
    }
}
