namespace RonFlow.Api.Domain;

public sealed record WorkflowStateModel(string Key, string Label, bool IsInitialState);

public sealed record ActivityTimelineItemModel(string Type, string Message, DateTimeOffset OccurredAt);

public sealed record TaskModel(
    Guid Id,
    Guid ProjectId,
    string Title,
    WorkflowStateModel CurrentState,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ActivityTimelineItemModel> ActivityTimeline);

public sealed record ProjectModel(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowStateModel> WorkflowStates);

public sealed record ProjectSummaryModel(Guid Id, string Name, DateTimeOffset UpdatedAt);

public sealed record ProjectBoardModel(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<WorkflowStateModel> WorkflowStates,
    IReadOnlyList<TaskModel> Tasks);

public sealed class RonFlowStore
{
    private static readonly WorkflowStateModel[] DefaultWorkflowStates =
    [
        new("todo", "待處理", true),
        new("active", "進行中", false),
        new("review", "審查中", false),
        new("done", "已完成", false),
    ];

    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, ProjectState> projects = [];

    public IReadOnlyList<ProjectSummaryModel> GetProjects()
    {
        lock (syncRoot)
        {
            return projects.Values
                .OrderByDescending(project => project.UpdatedAt)
                .Select(project => new ProjectSummaryModel(project.Id, project.Name, project.UpdatedAt))
                .ToArray();
        }
    }

    public ProjectModel CreateProject(string name)
    {
        lock (syncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectState(Guid.NewGuid(), name, now, DefaultWorkflowStates);
            projects.Add(project.Id, project);

            return project.ToProjectModel();
        }
    }

    public ProjectBoardModel? GetBoard(Guid projectId)
    {
        lock (syncRoot)
        {
            return projects.TryGetValue(projectId, out var project)
                ? project.ToBoardModel()
                : null;
        }
    }

    public TaskModel? CreateTask(Guid projectId, string title)
    {
        lock (syncRoot)
        {
            if (!projects.TryGetValue(projectId, out var project))
            {
                return null;
            }

            return project.CreateTask(title);
        }
    }

    public TaskModel? GetTask(Guid projectId, Guid taskId)
    {
        lock (syncRoot)
        {
            if (!projects.TryGetValue(projectId, out var project))
            {
                return null;
            }

            return project.GetTask(taskId);
        }
    }

    private sealed class ProjectState
    {
        private readonly List<TaskState> tasks = [];

        public ProjectState(Guid id, string name, DateTimeOffset updatedAt, IEnumerable<WorkflowStateModel> workflowStates)
        {
            Id = id;
            Name = name;
            UpdatedAt = updatedAt;
            WorkflowStates = workflowStates
                .Select(state => new WorkflowStateModel(state.Key, state.Label, state.IsInitialState))
                .ToArray();
        }

        public Guid Id { get; }

        public string Name { get; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public IReadOnlyList<WorkflowStateModel> WorkflowStates { get; }

        public ProjectModel ToProjectModel()
        {
            return new ProjectModel(Id, Name, UpdatedAt, WorkflowStates);
        }

        public ProjectBoardModel ToBoardModel()
        {
            return new ProjectBoardModel(
                Id,
                Name,
                WorkflowStates,
                tasks.Select(task => task.ToModel()).ToArray());
        }

        public TaskModel CreateTask(string title)
        {
            var now = DateTimeOffset.UtcNow;
            var initialState = WorkflowStates.First(state => state.IsInitialState);

            var task = new TaskState(
                Guid.NewGuid(),
                Id,
                title,
                initialState,
                now,
                [new ActivityTimelineItemModel("TaskCreated", "已建立任務", now)]);

            tasks.Add(task);
            UpdatedAt = now;

            return task.ToModel();
        }

        public TaskModel? GetTask(Guid taskId)
        {
            return tasks.FirstOrDefault(task => task.Id == taskId)?.ToModel();
        }
    }

    private sealed class TaskState
    {
        public TaskState(
            Guid id,
            Guid projectId,
            string title,
            WorkflowStateModel currentState,
            DateTimeOffset createdAt,
            IReadOnlyList<ActivityTimelineItemModel> activityTimeline)
        {
            Id = id;
            ProjectId = projectId;
            Title = title;
            CurrentState = currentState;
            CreatedAt = createdAt;
            ActivityTimeline = activityTimeline;
        }

        public Guid Id { get; }

        public Guid ProjectId { get; }

        public string Title { get; }

        public WorkflowStateModel CurrentState { get; }

        public DateTimeOffset CreatedAt { get; }

        public IReadOnlyList<ActivityTimelineItemModel> ActivityTimeline { get; }

        public TaskModel ToModel()
        {
            return new TaskModel(Id, ProjectId, Title, CurrentState, CreatedAt, ActivityTimeline);
        }
    }
}