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

public sealed record UpdateTaskResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied, bool Conflict)
{
    public static UpdateTaskResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false, false);
    }

    public static UpdateTaskResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false, false);
    }

    public static UpdateTaskResult NotFound()
    {
        return new(null, null, true, false, false);
    }

    public static UpdateTaskResult Denied()
    {
        return new(null, null, false, true, false);
    }

    public static UpdateTaskResult Locked()
    {
        return new(null, null, false, false, true);
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

public sealed record TaskLifecycleCommandResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied, bool Conflict)
{
    public static TaskLifecycleCommandResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false, false);
    }

    public static TaskLifecycleCommandResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false, false);
    }

    public static TaskLifecycleCommandResult NotFound()
    {
        return new(null, null, true, false, false);
    }

    public static TaskLifecycleCommandResult Denied()
    {
        return new(null, null, false, true, false);
    }

    public static TaskLifecycleCommandResult Locked()
    {
        return new(null, null, false, false, true);
    }
}

public sealed record CreateTaskReminderResult(CreateTaskOutput? Task, ValidationError? ValidationError, bool TaskNotFound, bool AccessDenied, bool Conflict)
{
    public static CreateTaskReminderResult Success(CreateTaskOutput task)
    {
        return new(task, null, false, false, false);
    }

    public static CreateTaskReminderResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false, false);
    }

    public static CreateTaskReminderResult NotFound()
    {
        return new(null, null, true, false, false);
    }

    public static CreateTaskReminderResult Denied()
    {
        return new(null, null, false, true, false);
    }

    public static CreateTaskReminderResult Locked()
    {
        return new(null, null, false, false, true);
    }
}

public sealed record DeleteTaskReminderResult(CreateTaskOutput? Task, bool TaskNotFound, bool ReminderNotFound, bool AccessDenied, bool Conflict)
{
    public static DeleteTaskReminderResult Success(CreateTaskOutput task)
    {
        return new(task, false, false, false, false);
    }

    public static DeleteTaskReminderResult TaskMissing()
    {
        return new(null, true, false, false, false);
    }

    public static DeleteTaskReminderResult ReminderMissing()
    {
        return new(null, false, true, false, false);
    }

    public static DeleteTaskReminderResult Denied()
    {
        return new(null, false, false, true, false);
    }

    public static DeleteTaskReminderResult Locked()
    {
        return new(null, false, false, false, true);
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

public sealed record CreateProjectInvitationResult(
    ProjectInvitationView? Invitation,
    ValidationError? ValidationError,
    bool ProjectNotFound,
    bool AccessDenied)
{
    public static CreateProjectInvitationResult Success(ProjectInvitationView invitation)
    {
        return new(invitation, null, false, false);
    }

    public static CreateProjectInvitationResult Invalid(string field, string message)
    {
        return new(null, new ValidationError(field, message), false, false);
    }

    public static CreateProjectInvitationResult NotFound()
    {
        return new(null, null, true, false);
    }

    public static CreateProjectInvitationResult Denied()
    {
        return new(null, null, false, true);
    }
}

public sealed record RespondToProjectInvitationResult(bool InvitationNotFound, bool AccessDenied)
{
    public static RespondToProjectInvitationResult Success()
    {
        return new(false, false);
    }

    public static RespondToProjectInvitationResult NotFound()
    {
        return new(true, false);
    }

    public static RespondToProjectInvitationResult Denied()
    {
        return new(false, true);
    }
}

public sealed record AcceptProjectInvitationResult(bool InvitationNotFound, bool AccessDenied, bool AlreadyHandled)
{
    public static AcceptProjectInvitationResult Success()
    {
        return new(false, false, false);
    }

    public static AcceptProjectInvitationResult NotFound()
    {
        return new(true, false, false);
    }

    public static AcceptProjectInvitationResult Denied()
    {
        return new(false, true, false);
    }

    public static AcceptProjectInvitationResult Conflict()
    {
        return new(false, false, true);
    }
}
