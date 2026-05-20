using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/reminders")]
public sealed class TaskReminderController : ControllerBase
{
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IResult CreateReminder(
        Guid projectId,
        Guid taskId,
        [FromBody] CreateTaskReminderRequest request,
        [FromServices] CreateTaskReminderCommandService commandService)
    {
        var result = commandService.Create(projectId, taskId, request.ReminderDateTime, request.Description);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.TaskNotFound)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "找不到指定的任務，無法新增提醒。");
        }

        return Results.Created(
            $"/api/projects/{projectId}/tasks/{taskId}/reminders",
            TaskDetailResponse.FromOutput(result.Task!));
    }

    [HttpDelete("{reminderId:guid}")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IResult DeleteReminder(
        Guid projectId,
        Guid taskId,
        Guid reminderId,
        [FromServices] DeleteTaskReminderCommandService commandService)
    {
        var result = commandService.Delete(projectId, taskId, reminderId);

        if (result.TaskNotFound)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "找不到指定的任務，無法刪除提醒。");
        }

        if (result.ReminderNotFound)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "找不到指定的提醒，無法刪除。");
        }

        return Results.Ok(TaskDetailResponse.FromOutput(result.Task!));
    }
}