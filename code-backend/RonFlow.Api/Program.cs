
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
        builder.Services.AddControllers();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        ConfigurePersistence(builder);
        builder.Services.AddSingleton<CreateProjectCommandService>();
        builder.Services.AddSingleton<CreateTaskCommandService>();
        builder.Services.AddSingleton<ChangeTaskStateCommandService>();
        builder.Services.AddSingleton<UpdateTaskCommandService>();
        builder.Services.AddSingleton<ReorderTaskCommandService>();
        builder.Services.AddSingleton<ArchiveTaskCommandService>();
        builder.Services.AddSingleton<RestoreArchivedTaskCommandService>();
        builder.Services.AddSingleton<MoveTaskToTrashCommandService>();
        builder.Services.AddSingleton<RestoreTrashedTaskCommandService>();
        builder.Services.AddSingleton<GetProjectsQueryService>();
        builder.Services.AddSingleton<GetProjectBoardQueryService>();
        builder.Services.AddSingleton<GetTaskDetailQueryService>();
        builder.Services.AddSingleton<GetArchivedTasksQueryService>();
        builder.Services.AddSingleton<GetTrashedTasksQueryService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapControllers();

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
