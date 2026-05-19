using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Endpoints;

internal static class TaskLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapTaskLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{projectId:guid}/tasks/archived", (Guid projectId, GetArchivedTasksQueryService queryService) =>
        {
            var tasks = queryService.Get(projectId);
            return tasks is null ? Results.NotFound() : Results.Ok(LifecycleTaskListResponse.FromView(tasks));
        })
        .Produces<LifecycleTaskListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/projects/{projectId:guid}/tasks/trashed", (Guid projectId, GetTrashedTasksQueryService queryService) =>
        {
            var tasks = queryService.Get(projectId);
            return tasks is null ? Results.NotFound() : Results.Ok(LifecycleTaskListResponse.FromView(tasks));
        })
        .Produces<LifecycleTaskListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}/archive", (
            Guid projectId,
            Guid taskId,
            ArchiveTaskCommandService commandService) =>
        {
            var result = commandService.Archive(projectId, taskId);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}/restore-from-archive", (
            Guid projectId,
            Guid taskId,
            RestoreArchivedTaskCommandService commandService) =>
        {
            var result = commandService.Restore(projectId, taskId);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}/trash", (
            Guid projectId,
            Guid taskId,
            MoveTaskToTrashCommandService commandService) =>
        {
            var result = commandService.Move(projectId, taskId);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}/restore-from-trash", (
            Guid projectId,
            Guid taskId,
            RestoreTrashedTaskCommandService commandService) =>
        {
            var result = commandService.Restore(projectId, taskId);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}