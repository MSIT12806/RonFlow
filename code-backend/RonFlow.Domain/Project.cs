namespace RonFlow.Domain;

public sealed class Project
{
    private readonly IReadOnlyList<WorkflowState> workflowStates;

    private Project(Guid id, Guid ownerId, string name, DateTimeOffset updatedAt, IEnumerable<WorkflowState> workflowStates)
    {
        Id = id;
        OwnerId = ownerId;
        Name = name;
        UpdatedAt = updatedAt;
        this.workflowStates = workflowStates
            .Select(state => new WorkflowState(state.Key, state.Label, state.IsInitialState, state.IsCompletedState))
            .ToArray();
    }

    public Guid Id { get; }

    public Guid OwnerId { get; }

    public string Name { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<WorkflowState> WorkflowStates => workflowStates;

    public static Project Create(Guid ownerId, ProjectName name, DateTimeOffset createdAt, IEnumerable<WorkflowState> workflowStates)
    {
        return new Project(Guid.NewGuid(), ownerId, name.Value, createdAt, workflowStates);
    }

    public static Project Create(ProjectName name, DateTimeOffset createdAt, IEnumerable<WorkflowState> workflowStates)
    {
        return Create(Guid.Empty, name, createdAt, workflowStates);
    }

    public static Project Rehydrate(Guid id, Guid ownerId, string name, DateTimeOffset updatedAt, IEnumerable<WorkflowState> workflowStates)
    {
        return new Project(id, ownerId, name, updatedAt, workflowStates);
    }

    public ProjectModel ToModel()
    {
        return new ProjectModel(Id, OwnerId, Name, UpdatedAt, workflowStates.Select(state => state.ToModel()).ToArray());
    }

    public ProjectSummaryModel ToSummaryModel()
    {
        return new ProjectSummaryModel(Id, OwnerId, Name, UpdatedAt);
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

    public WorkflowState? FindWorkflowState(string stateKey)
    {
        return workflowStates.FirstOrDefault(state => state.Key == stateKey);
    }

    public void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}