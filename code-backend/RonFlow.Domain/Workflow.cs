namespace RonFlow.Domain;

public sealed record WorkflowState(string Key, string Label, bool IsInitialState, bool IsCompletedState)
{
    public WorkflowStateModel ToModel()
    {
        return new(Key, Label, IsInitialState, IsCompletedState);
    }
}

public static class DefaultWorkflow
{
    public static IReadOnlyList<WorkflowState> CreateStates()
    {
        return
        [
            new WorkflowState("todo", "待處理", true, false),
            new WorkflowState("active", "進行中", false, false),
            new WorkflowState("review", "審查中", false, false),
            new WorkflowState("done", "已完成", false, true),
        ];
    }
}