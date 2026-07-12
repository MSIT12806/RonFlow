using RonFlow.Application;
using RonFlow.Domain;
using RonFlow.Infrastructure;
using RonFlow.Observability;
using RonFlow.Testing.Infrastructure;

namespace RonFlow.Api;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRonFlowPlatformServices(
        this IServiceCollection services,
        IHostEnvironment environment,
        ConfigurationManager configuration)
    {
        services.AddScoped<ObservedOperationServerTimingFilter>();
        services.AddScoped<ObservedOperationResultTimingFilter>();
        services.AddSingleton<ITestHttpFaultStore>(environment.IsEnvironment("Testing")
            ? new InMemoryTestHttpFaultStore()
            : new NoOpTestHttpFaultStore());
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(_ => PushNotificationConfiguration.Create(
            configuration["PushNotifications:Subject"],
            configuration["PushNotifications:PublicKey"],
            configuration["PushNotifications:PrivateKey"]));
        services.AddSingleton<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
        services.AddSingleton<IDomainEventHandler, DatabaseSyncDomainEventHandler>();

        return services;
    }

    public static IServiceCollection AddRonFlowPersistence(
        this IServiceCollection services,
        IHostEnvironment environment,
        ConfigurationManager configuration)
    {
        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IDatabaseSyncCoordinator>(NoOpDatabaseSyncCoordinator.Instance);
            services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
            services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            services.AddSingleton<IPushSubscriptionRepository, InMemoryPushSubscriptionRepository>();
            services.AddSingleton<IAiAuditProjectionOutbox, InMemoryAiAuditProjectionOutbox>();
            services.AddSingleton<IAiAuditReadModelStore, InMemoryAiAuditReadModelStore>();
            services.AddSingleton<IWorkflowThroughputProjectionOutbox, InMemoryWorkflowThroughputProjectionOutbox>();
            services.AddSingleton<IWorkflowThroughputProjectionStore, InMemoryWorkflowThroughputProjectionStore>();
            services.AddSingleton<InMemoryCoreFlowReadStore>();
            services.AddSingleton<ICoreFlowReadStore>(serviceProvider =>
                new ObservedCoreFlowReadStore(serviceProvider.GetRequiredService<InMemoryCoreFlowReadStore>()));
            services.AddSingleton<IUserDirectory, InMemoryUserDirectory>();
            return services;
        }

        var configuredDatabasePath = configuration["Persistence:Sqlite:DatabasePath"];
        var databasePath = string.IsNullOrWhiteSpace(configuredDatabasePath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "ronflow.db")
            : ResolveDatabasePath(environment.ContentRootPath, configuredDatabasePath);

        var databaseSyncOptions = CreateDatabaseSyncOptions(environment.ContentRootPath, configuration, databasePath);
        services.AddSingleton(databaseSyncOptions);
        services.AddSingleton<IDatabaseSyncCoordinator>(serviceProvider =>
        {
            IDatabaseSyncCoordinator databaseSyncCoordinator;
            if (databaseSyncOptions.Enabled)
            {
                serviceProvider.GetRequiredService<ILogger<Program>>().LogInformation(
                    "RonFlow database Git sync is enabled. RuntimeDatabasePath: {RuntimeDatabasePath}; RepositoryPath: {RepositoryPath}; RemoteUrlConfigured: {RemoteUrlConfigured}; Branch: {Branch}; DatabaseFileName: {DatabaseFileName}; GitCommandTimeoutSeconds: {GitCommandTimeoutSeconds}",
                    databaseSyncOptions.RuntimeDatabasePath,
                    databaseSyncOptions.RepositoryPath,
                    !string.IsNullOrWhiteSpace(databaseSyncOptions.RemoteUrl),
                    databaseSyncOptions.Branch,
                    databaseSyncOptions.DatabaseFileName,
                    databaseSyncOptions.GitCommandTimeoutSeconds);

                databaseSyncCoordinator = new DatabaseSyncCoordinator(
                    databaseSyncOptions,
                    new SqliteDatabaseSnapshotStore(),
                    new GitDatabaseRepositorySync(databaseSyncOptions),
                    new DbMergerDatabaseSnapshotMerger(),
                    serviceProvider.GetRequiredService<ILogger<DatabaseSyncCoordinator>>());
            }
            else
            {
                serviceProvider.GetRequiredService<ILogger<Program>>().LogWarning(
                    "RonFlow database Git sync is disabled. RuntimeDatabasePath: {RuntimeDatabasePath}; RepositoryPath: {RepositoryPath}; RemoteUrlConfigured: {RemoteUrlConfigured}; Branch: {Branch}; DatabaseFileName: {DatabaseFileName}",
                    databaseSyncOptions.RuntimeDatabasePath,
                    databaseSyncOptions.RepositoryPath,
                    !string.IsNullOrWhiteSpace(databaseSyncOptions.RemoteUrl),
                    databaseSyncOptions.Branch,
                    databaseSyncOptions.DatabaseFileName);
                databaseSyncCoordinator = NoOpDatabaseSyncCoordinator.Instance;
            }

            databaseSyncCoordinator.PullBeforeOpen();
            return databaseSyncCoordinator;
        });
        services.AddSingleton(serviceProvider =>
            new SqliteCoreFlowStore(databasePath, serviceProvider.GetRequiredService<IDomainEventDispatcher>()));
        services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<IPushSubscriptionRepository, SqlitePushSubscriptionRepository>();
        services.AddSingleton<IAiAuditProjectionOutbox, SqliteAiAuditProjectionOutbox>();
        services.AddSingleton<IAiAuditReadModelStore, SqliteAiAuditReadModelStore>();
        services.AddSingleton<IWorkflowThroughputProjectionOutbox, SqliteWorkflowThroughputProjectionOutbox>();
        services.AddSingleton<IWorkflowThroughputProjectionStore, SqliteWorkflowThroughputProjectionStore>();
        services.AddSingleton<SqliteCoreFlowReadStore>();
        services.AddSingleton<ICoreFlowReadStore>(serviceProvider =>
            new ObservedCoreFlowReadStore(serviceProvider.GetRequiredService<SqliteCoreFlowReadStore>()));
        services.AddSingleton<IUserDirectory, SqliteUserDirectory>();

        return services;
    }

    public static IServiceCollection AddRonFlowCommandServices(this IServiceCollection services)
    {
        services.AddSingleton<ProjectAccessService>();
        services.AddSingleton<TaskContentEditLockService>();
        services.AddSingleton<TaskMutationGuard>();
        services.AddSingleton<ProjectPresenceRegistry>();
        services.AddSingleton<AiAuditRegistry>();
        services.AddSingleton<ProcessAiAuditProjectionService>();
        services.AddSingleton<RonFlowActiveSessionRegistry>();
        services.AddSingleton<ProcessWorkflowThroughputProjectionService>();
        services.AddSingleton<IPushNotificationSender, WebPushNotificationSender>();

        services.AddSingleton<CreateProjectCommandService>();
        services.AddSingleton<CreateTaskCommandService>();
        services.AddSingleton<CreateChildTaskCommandService>();
        services.AddSingleton<ReplaceProjectSubtaskTemplatesCommandService>();
        services.AddSingleton<ReplaceTaskSubtasksCommandService>();
        services.AddSingleton<ChangeTaskStateCommandService>();
        services.AddSingleton<UpdateTaskCommandService>();
        services.AddSingleton<SetTaskSplitCompleteCommandService>();
        services.AddSingleton<ReorderTaskCommandService>();
        services.AddSingleton<CreateTaskReminderCommandService>();
        services.AddSingleton<DeleteTaskReminderCommandService>();
        services.AddSingleton<RegisterPushSubscriptionCommandService>();
        services.AddSingleton<DeliverDueReminderNotificationsCommandService>();
        services.AddSingleton<ArchiveTaskCommandService>();
        services.AddSingleton<RestoreArchivedTaskCommandService>();
        services.AddSingleton<MoveTaskToTrashCommandService>();
        services.AddSingleton<RestoreTrashedTaskCommandService>();
        services.AddSingleton<ProjectInvitationCommandService>();

        return services;
    }

    public static IServiceCollection AddRonFlowQueryServices(this IServiceCollection services)
    {
        services.AddSingleton<GetProjectsQueryService>();
        services.AddSingleton<GetProjectBoardQueryService>();
        services.AddSingleton<GetProjectCodeTraceabilityQueryService>();
        services.AddSingleton<GetProjectSubtaskTemplatesQueryService>();
        services.AddSingleton<IGetProjectBoardQueryService>(serviceProvider =>
            new ObservedGetProjectBoardQueryService(serviceProvider.GetRequiredService<GetProjectBoardQueryService>()));
        services.AddSingleton<ProjectCollaborationQueryService>();
        services.AddSingleton<GetTaskDetailQueryService>();
        services.AddSingleton<GetWorkflowThroughputReportQueryService>();
        services.AddSingleton<GetTaskAgingReportQueryService>();
        services.AddSingleton<GetCycleTimeReportQueryService>();
        services.AddSingleton<GetCompletedTasksByMonthReportQueryService>();
        services.AddSingleton<GetArchivedTasksQueryService>();
        services.AddSingleton<GetTrashedTasksQueryService>();

        return services;
    }

    public static IServiceCollection AddRonFlowBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<ReminderNotificationBackgroundService>();
        services.AddHostedService<AiAuditProjectionBackgroundService>();
        services.AddHostedService<WorkflowThroughputProjectionBackgroundService>();
        services.AddHostedService<DatabaseSyncBackgroundService>();

        return services;
    }

    private static string ResolveDatabasePath(string contentRootPath, string configuredDatabasePath)
    {
        return Path.IsPathRooted(configuredDatabasePath)
            ? configuredDatabasePath
            : Path.Combine(contentRootPath, configuredDatabasePath);
    }

    private static DatabaseSyncOptions CreateDatabaseSyncOptions(
        string contentRootPath,
        ConfigurationManager configuration,
        string databasePath)
    {
        var section = configuration.GetSection("Persistence:DatabaseGitSync");
        var enabled = section.GetValue<bool>("Enabled");
        var configuredRepositoryPath = section["RepositoryPath"];
        var repositoryPath = string.IsNullOrWhiteSpace(configuredRepositoryPath)
            ? Path.Combine(contentRootPath, "App_Data", "ronflow-db-repository")
            : ResolveDatabasePath(contentRootPath, configuredRepositoryPath);
        var configuredBranch = section["Branch"];
        var configuredDatabaseFileName = section["DatabaseFileName"];
        var configuredRemoteUrl = section["RemoteUrl"];
        var configuredAccessToken = section["AccessToken"];
        var configuredGitCommandTimeoutSeconds = section.GetValue<int?>("GitCommandTimeoutSeconds");

        return new DatabaseSyncOptions
        {
            Enabled = enabled,
            RuntimeDatabasePath = databasePath,
            RepositoryPath = repositoryPath,
            RemoteUrl = string.IsNullOrWhiteSpace(configuredRemoteUrl) ? null : configuredRemoteUrl,
            AccessToken = string.IsNullOrWhiteSpace(configuredAccessToken) ? null : configuredAccessToken,
            Branch = string.IsNullOrWhiteSpace(configuredBranch) ? "main" : configuredBranch,
            DatabaseFileName = string.IsNullOrWhiteSpace(configuredDatabaseFileName) ? "ronflow.db" : configuredDatabaseFileName,
            GitCommandTimeoutSeconds = configuredGitCommandTimeoutSeconds.GetValueOrDefault(30),
        };
    }
}
