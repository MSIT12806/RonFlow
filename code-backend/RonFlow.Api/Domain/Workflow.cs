namespace RonFlow.Domain;

public sealed record WorkflowState(string Key, string Label, bool IsInitialState)
{
    public WorkflowStateModel ToModel()
    {
        return new(Key, Label, IsInitialState);
    }
}

public static class DefaultWorkflow
{
    public static IReadOnlyList<WorkflowState> CreateStates()
    {
        return
        [
            new WorkflowState("todo", "待處理", true),
            new WorkflowState("active", "進行中", false),
            new WorkflowState("review", "審查中", false),
            new WorkflowState("done", "已完成", false),
        ];
    }
}