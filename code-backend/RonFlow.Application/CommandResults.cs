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

public sealed record CreateTaskResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool ProjectNotFound, bool AccessDenied)
{
    public static CreateTaskResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false);
    }

    public static CreateTaskResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false);
    }

    public static CreateTaskResult NotFound()
    {
        return new(null, null, true, false);
    }

    public static CreateTaskResult Denied()
    {
        return new(null, null, false, true);
    }
}

public sealed record ChangeTaskStateResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied)
{
    public static ChangeTaskStateResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false);
    }

    public static ChangeTaskStateResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false);
    }

    public static ChangeTaskStateResult NotFound()
    {
        return new(null, null, true, false);
    }

    public static ChangeTaskStateResult Denied()
    {
        return new(null, null, false, true);
    }
}

public sealed record UpdateTaskResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied)
{
    public static UpdateTaskResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false);
    }

    public static UpdateTaskResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false);
    }

    public static UpdateTaskResult NotFound()
    {
        return new(null, null, true, false);
    }

    public static UpdateTaskResult Denied()
    {
        return new(null, null, false, true);
    }
}

public sealed record ReorderTaskResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied)
{
    public static ReorderTaskResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false);
    }

    public static ReorderTaskResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false);
    }

    public static ReorderTaskResult NotFound()
    {
        return new(null, null, true, false);
    }

    public static ReorderTaskResult Denied()
    {
        return new(null, null, false, true);
    }
}

public sealed record TaskLifecycleCommandResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied)
{
    public static TaskLifecycleCommandResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false);
    }

    public static TaskLifecycleCommandResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false);
    }

    public static TaskLifecycleCommandResult NotFound()
    {
        return new(null, null, true, false);
    }

    public static TaskLifecycleCommandResult Denied()
    {
        return new(null, null, false, true);
    }
}

public sealed record CreateTaskReminderResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied)
{
    public static CreateTaskReminderResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false);
    }

    public static CreateTaskReminderResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false);
    }

    public static CreateTaskReminderResult NotFound()
    {
        return new(null, null, true, false);
    }

    public static CreateTaskReminderResult Denied()
    {
        return new(null, null, false, true);
    }
}

public sealed record DeleteTaskReminderResult(CreateTaskOutput? Task, bool TaskNotFound, bool ReminderNotFound, bool AccessDenied)
{
    public static DeleteTaskReminderResult Success(CreateTaskOutput task)
    {
        return new(task, false, false, false);
    }

    public static DeleteTaskReminderResult TaskMissing()
    {
        return new(null, true, false, false);
    }

    public static DeleteTaskReminderResult ReminderMissing()
    {
        return new(null, false, true, false);
    }

    public static DeleteTaskReminderResult Denied()
    {
        return new(null, false, false, true);
    }
}

public sealed record RegisterPushSubscriptionResult(ValidationError? ValidationError)
{
    public static RegisterPushSubscriptionResult Success()
    {
        return new((ValidationError?)null);
    }

    public static RegisterPushSubscriptionResult Invalid(string field, string message)
    {
        return new(new ValidationError(field, message));
    }
}
