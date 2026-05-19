using System.Net.Mime;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Endpoints;

internal static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/projects/{projectId:guid}/tasks", (Guid projectId, CreateTaskRequest request, CreateTaskCommandService commandService) =>
        {
            var result = commandService.Create(projectId, request.Title);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            if (result.ProjectNotFound)
            {
                return Results.NotFound();
            }

            var task = result.Task!;

            return Results.Created($"/api/projects/{projectId}/tasks/{task.Id}", TaskDetailResponse.FromOutput(task));
        })
        .Accepts<CreateTaskRequest>(MediaTypeNames.Application.Json)
        .Produces<TaskDetailResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}/state", (
            Guid projectId,
            Guid taskId,
            ChangeTaskStateRequest request,
            ChangeTaskStateCommandService commandService) =>
        {
            var result = commandService.Change(projectId, taskId, request.StateKey ?? string.Empty);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Accepts<ChangeTaskStateRequest>(MediaTypeNames.Application.Json)
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}", (
            Guid projectId,
            Guid taskId,
            UpdateTaskRequest request,
            UpdateTaskCommandService commandService) =>
        {
            var result = commandService.Update(projectId, taskId, request.Title, request.Description, request.DueDate);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Accepts<UpdateTaskRequest>(MediaTypeNames.Application.Json)
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/projects/{projectId:guid}/tasks/{taskId:guid}/order", (
            Guid projectId,
            Guid taskId,
            ReorderTaskRequest request,
            ReorderTaskCommandService commandService) =>
        {
            if (request.TargetTaskId is null)
            {
                return ValidationResults.FromError(new ValidationError("targetTaskId", "目標任務為必填欄位"));
            }

            var result = commandService.Reorder(projectId, taskId, request.TargetTaskId.Value);

            if (result.ValidationError is not null)
            {
                return ValidationResults.FromError(result.ValidationError);
            }

            return result.TaskNotFound
                ? Results.NotFound()
                : Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
        })
        .Accepts<ReorderTaskRequest>(MediaTypeNames.Application.Json)
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/projects/{projectId:guid}/tasks/{taskId:guid}", (Guid projectId, Guid taskId, GetTaskDetailQueryService queryService) =>
        {
            var task = queryService.Get(projectId, taskId);
            return task is null ? Results.NotFound() : Results.Ok(TaskDetailResponse.FromView(task));
        })
        .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}