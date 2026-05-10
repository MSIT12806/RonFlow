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
        var task = Task.Create(Id, title, GetDefaultWorkflowState(), createdAt);
        tasks.Add(task);
        UpdatedAt = createdAt;

        return task;
    }

    public Task? GetTask(Guid taskId)
    {
        return tasks.FirstOrDefault(task => task.Id == taskId);
    }

    /// <summary>
    /// 取得這個 Project Task 建立的預設 WorkflowState
    /// </summary>
    public WorkflowState GetDefaultWorkflowState()
    {
        var initialState = workflowStates.First(state => state.IsInitialState);
        return initialState;
    }

    public WorkflowState GetWorkflowState(string stateKey)
    {
        return workflowStates.First(state => state.Key == stateKey);
    }

    public Task? ChangeTaskState(Guid taskId, string stateKey, DateTimeOffset changedAt)
    {
        var task = GetTask(taskId);
        if (task is null)
        {
            return null;
        }

        var targetState = workflowStates.FirstOrDefault(state => state.Key == stateKey);
        if (targetState is null)
        {
            return null;
        }

        if (task.ChangeState(targetState, changedAt))
        {
            UpdatedAt = changedAt;
        }

        return task;
    }
}