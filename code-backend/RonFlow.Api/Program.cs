using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using RonFlow.Application;
using RonFlow.Api.Contracts;
using RonFlow.Domain;
using RonFlow.Infrastructure;
using RonFlow.Observability;

namespace RonFlow.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddControllers();
        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });
        builder.Services
            .AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(RonFlowObservabilityMetrics.MeterName)
                .AddPrometheusExporter());
        builder.Services.AddScoped<ObservedOperationServerTimingFilter>();
        builder.Services.AddScoped<ObservedOperationResultTimingFilter>();
        builder.Services.AddSingleton<ITestHttpFaultStore>(builder.Environment.IsEnvironment("Testing")
            ? new InMemoryTestHttpFaultStore()
            : new NoOpTestHttpFaultStore());
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton(_ => PushNotificationConfiguration.Create(
            builder.Configuration["PushNotifications:Subject"],
            builder.Configuration["PushNotifications:PublicKey"],
            builder.Configuration["PushNotifications:PrivateKey"]));
        var ronAuthOptions = builder.Configuration.GetSection(RonAuthAuthenticationOptions.SectionName).Get<RonAuthAuthenticationOptions>()
            ?? new RonAuthAuthenticationOptions();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ronAuthOptions.SigningKey));
        builder.Services.Configure<RonAuthAuthenticationOptions>(builder.Configuration.GetSection(RonAuthAuthenticationOptions.SectionName));
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = ronAuthOptions.Issuer,
                    ValidAudience = ronAuthOptions.Audience,
                    IssuerSigningKey = signingKey,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.Zero,
                };
            });
        builder.Services.AddAuthorization();
        ConfigurePersistence(builder);
        builder.Services.AddSingleton<ProjectAccessService>();
        builder.Services.AddSingleton<CreateProjectCommandService>();
        builder.Services.AddSingleton<CreateTaskCommandService>();
        builder.Services.AddSingleton<ChangeTaskStateCommandService>();
        builder.Services.AddSingleton<UpdateTaskCommandService>();
        builder.Services.AddSingleton<TaskContentEditLockService>();
        builder.Services.AddSingleton<ProjectPresenceRegistry>();
        builder.Services.AddSingleton<AiAuditRegistry>();
        builder.Services.AddSingleton<RonFlowActiveSessionRegistry>();
        builder.Services.AddSingleton<ReorderTaskCommandService>();
        builder.Services.AddSingleton<CreateTaskReminderCommandService>();
        builder.Services.AddSingleton<DeleteTaskReminderCommandService>();
        builder.Services.AddSingleton<RegisterPushSubscriptionCommandService>();
        builder.Services.AddSingleton<DeliverDueReminderNotificationsCommandService>();
        builder.Services.AddSingleton<ArchiveTaskCommandService>();
        builder.Services.AddSingleton<RestoreArchivedTaskCommandService>();
        builder.Services.AddSingleton<MoveTaskToTrashCommandService>();
        builder.Services.AddSingleton<RestoreTrashedTaskCommandService>();
        builder.Services.AddSingleton<ProjectInvitationCommandService>();
        builder.Services.AddSingleton<IPushNotificationSender, WebPushNotificationSender>();
        builder.Services.AddSingleton<GetProjectsQueryService>();
        builder.Services.AddSingleton<GetProjectBoardQueryService>();
        builder.Services.AddSingleton<IGetProjectBoardQueryService>(serviceProvider =>
            new ObservedGetProjectBoardQueryService(serviceProvider.GetRequiredService<GetProjectBoardQueryService>()));
        builder.Services.AddSingleton<ProjectCollaborationQueryService>();
        builder.Services.AddSingleton<GetTaskDetailQueryService>();
        builder.Services.AddSingleton<GetArchivedTasksQueryService>();
        builder.Services.AddSingleton<GetTrashedTasksQueryService>();
        builder.Services.AddHostedService<ReminderNotificationBackgroundService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseAuthentication();
        app.UseHttpLogging();
        app.UseMiddleware<ObservedOperationTimingMiddleware>();
        app.UseMiddleware<CurrentUserDirectorySyncMiddleware>();
        app.UseMiddleware<TestHttpFaultMiddleware>();
        app.UseMiddleware<RonFlowActiveSessionMiddleware>();
        app.UseAuthorization();
        app.MapPrometheusScrapingEndpoint("/metrics");
        app.MapControllers();

        app.Run();
    }

    private static void ConfigurePersistence(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
            builder.Services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            builder.Services.AddSingleton<IPushSubscriptionRepository, InMemoryPushSubscriptionRepository>();
            builder.Services.AddSingleton<InMemoryCoreFlowReadStore>();
            builder.Services.AddSingleton<ICoreFlowReadStore>(serviceProvider =>
                new ObservedCoreFlowReadStore(serviceProvider.GetRequiredService<InMemoryCoreFlowReadStore>()));
            builder.Services.AddSingleton<IUserDirectory, InMemoryUserDirectory>();
            return;
        }

        var configuredDatabasePath = builder.Configuration["Persistence:Sqlite:DatabasePath"];
        var databasePath = string.IsNullOrWhiteSpace(configuredDatabasePath)
            ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "ronflow.db")
            : ResolveDatabasePath(builder.Environment.ContentRootPath, configuredDatabasePath);

        builder.Services.AddSingleton(new SqliteCoreFlowStore(databasePath));
        builder.Services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
        builder.Services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        builder.Services.AddSingleton<IPushSubscriptionRepository, SqlitePushSubscriptionRepository>();
        builder.Services.AddSingleton<SqliteCoreFlowReadStore>();
        builder.Services.AddSingleton<ICoreFlowReadStore>(serviceProvider =>
            new ObservedCoreFlowReadStore(serviceProvider.GetRequiredService<SqliteCoreFlowReadStore>()));
        builder.Services.AddSingleton<IUserDirectory, SqliteUserDirectory>();
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
