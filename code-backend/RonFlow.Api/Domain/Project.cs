namespace RonFlow.Domain;

public sealed class Project
{
    private readonly List<Task> tasks = [];
    private readonly IReadOnlyList<WorkflowState> workflowStates;

    private Project(Guid id, string name, DateTimeOffset updatedAt, IEnumerable<WorkflowState> workflowStates)
    {
        Id = id;
        Name = name;
        UpdatedAt = updatedAt;
        this.workflowStates = workflowStates
            .Select(state => new WorkflowState(state.Key, state.Label, state.IsInitialState))
            .ToArray();
    }

    public Guid Id { get; }

    public string Name { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Project Create(ProjectName name, DateTimeOffset createdAt, IEnumerable<WorkflowState> workflowStates)
    {
        return new Project(Guid.NewGuid(), name.Value, createdAt, workflowStates);
    }

    public ProjectModel ToModel()
    {
        return new ProjectModel(Id, Name, UpdatedAt, workflowStates.Select(state => state.ToModel()).ToArray());
    }

    public ProjectSummaryModel ToSummaryModel()
    {
        return new ProjectSummaryModel(Id, Name, UpdatedAt);
    }

    public ProjectBoardModel ToBoardModel()
    {
        return new ProjectBoardModel(
            Id,
            Name,
            workflowStates.Select(state => state.ToModel()).ToArray(),
            tasks.Select(task => task.ToModel()).ToArray());
    }

    public Task CreateTask(TaskTitle title, DateTimeOffset createdAt)
    {
        var initialState = workflowStates.First(state => state.IsInitialState);
        var task = Task.Create(Id, title, initialState, createdAt);

        tasks.Add(task);
        UpdatedAt = createdAt;

        return task;
    }

    public Task? GetTask(Guid taskId)
    {
        return tasks.FirstOrDefault(task => task.Id == taskId);
    }
}