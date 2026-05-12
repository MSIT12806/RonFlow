namespace RonFlow.Application;

public sealed record ValidationError(string Field, string Message);

public sealed record CreateProjectResult(CreateProjectOutput? Project, ValidationError? ValidationError)
{
    public static CreateProjectResult Success(CreateProjectOutput project)
    {
        return new(project, null);
    }

    public static CreateProjectResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message));
    }
}

public sealed record CreateTaskResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool ProjectNotFound)
{
    public static CreateTaskResult Success(CreateTaskOutput task)
    {
        return new(task, null, false);
    }

    public static CreateTaskResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false);
    }

    public static CreateTaskResult NotFound()
    {
        return new(null, null, true);
    }
}

public sealed record ChangeTaskStateResult(CreateTaskOutput? Task, bool TaskNotFound)
{
    public static ChangeTaskStateResult Success(CreateTaskOutput task)
    {
        return new(task, false);
    }

    public static ChangeTaskStateResult NotFound()
    {
        return new(null, true);
    }
}

public sealed record UpdateTaskResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound)
{
    public static UpdateTaskResult Success(CreateTaskOutput task)
    {
        return new(task, null, false);
    }

    public static UpdateTaskResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false);
    }

    public static UpdateTaskResult NotFound()
    {
        return new(null, null, true);
    }
}

public sealed record ReorderTaskResult(CreateTaskOutput? Task, bool TaskNotFound)
{
    public static ReorderTaskResult Success(CreateTaskOutput task)
    {
        return new(task, false);
    }

    public static ReorderTaskResult NotFound()
    {
        return new(null, true);
    }
}
