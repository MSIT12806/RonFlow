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
        var corsOrigins = GetAllowedCorsOrigins(builder.Configuration);

        builder.Services.AddOpenApi();
        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IDatabaseSyncInitiatorContext, HttpContextDatabaseSyncInitiatorContext>();
        builder.Services.AddSingleton<IDatabaseSyncNotificationPublisher, SignalRDatabaseSyncNotificationPublisher>();
        builder.Services.AddSingleton<ITaskNotificationPublisher, SignalRTaskNotificationPublisher>();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy.WithOrigins(corsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
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
        builder.Services.AddRonFlowPlatformServices(builder.Environment, builder.Configuration);
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
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(DatabaseSyncNotificationHub.Route))
                        {
                            context.Token = accessToken;
                        }

                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddRonFlowPersistence(builder.Environment, builder.Configuration);
        builder.Services.AddRonFlowCommandServices();
        builder.Services.AddRonFlowQueryServices();
        builder.Services.AddRonFlowBackgroundServices();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseCors("Frontend");
        app.UseAuthentication();
        app.UseMiddleware<RuntimeDatabaseAccessMiddleware>();
        app.UseHttpLogging();
        app.UseMiddleware<DatabaseSyncRequestUpdateMiddleware>();
        app.UseMiddleware<ObservedOperationTimingMiddleware>();
        app.UseMiddleware<CurrentUserDirectorySyncMiddleware>();
        app.UseMiddleware<TestHttpFaultMiddleware>();
        app.UseMiddleware<RonFlowActiveSessionMiddleware>();
        app.UseAuthorization();
        app.MapPrometheusScrapingEndpoint("/metrics");
        app.MapControllers();
        app.MapHub<DatabaseSyncNotificationHub>(DatabaseSyncNotificationHub.Route);

        app.Run();
    }

    private static string[] GetAllowedCorsOrigins(ConfigurationManager configuration)
    {
        var configuredOrigins = configuration["Cors:AllowedOrigins"];
        if (string.IsNullOrWhiteSpace(configuredOrigins))
        {
            return ["http://localhost", "http://127.0.0.1", "http://localhost:80", "http://127.0.0.1:80"];
        }

        return configuredOrigins
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
