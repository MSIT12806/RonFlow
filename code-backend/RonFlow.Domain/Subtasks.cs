namespace RonFlow.Domain;

public sealed record ProjectSubtaskTemplate(Guid Id, string Title, int Order)
{
    public ProjectSubtaskTemplateModel ToModel()
    {
        return new(Id, Title, Order);
    }
}

public sealed record TaskSubtask(Guid Id, string Title, bool IsChecked, int Order)
{
    public TaskSubtaskModel ToModel()
    {
        return new(Id, Title, IsChecked, Order);
    }
}