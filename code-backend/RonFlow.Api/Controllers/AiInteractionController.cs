using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using System.Text;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public sealed class AiInteractionController : AuthenticatedControllerBase
{
    [HttpGet("bootstrap")]
    [Produces("text/plain")]
    public IResult GetBootstrap()
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return PlainText(AiTextContractFormatter.Bootstrap());
    }

    [HttpGet("capabilities")]
    [Produces("text/plain")]
    public IResult GetCapabilities()
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return PlainText(AiTextContractFormatter.CapabilitiesManifest());
    }

    [HttpGet("workflow-guidance")]
    [Produces("text/plain")]
    public IResult GetWorkflowGuidance()
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Results.Unauthorized();
        }

        return PlainText(AiTextContractFormatter.WorkflowGuidance());
    }

    [HttpGet("session-summary")]
    [Produces("text/plain")]
    public IResult GetSessionSummary(
        [FromServices] GetProjectsQueryService getProjectsQueryService,
        [FromServices] ProjectPresenceRegistry projectPresenceRegistry)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || !TryGetCurrentUserName(out var currentUserName))
        {
            return Results.Unauthorized();
        }

        var availableScopes = getProjectsQueryService.Get(currentUserId).Items
            .Select(item => item.Id)
            .ToArray();
        var activeScope = TryGetRonFlowSessionId(out var sessionId)
            ? projectPresenceRegistry.GetActiveProjectScope(sessionId)
            : null;

        return PlainText(AiTextContractFormatter.SessionSummary(currentUserName, activeScope, availableScopes));
    }

    [HttpGet("projects/summary")]
    [Produces("text/plain")]
    public IResult GetProjectListSummary(
        [FromServices] GetProjectsQueryService getProjectsQueryService,
        [FromServices] GetProjectBoardQueryService getProjectBoardQueryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var projects = getProjectsQueryService.Get(currentUserId);

        return PlainText(AiTextContractFormatter.ProjectListSummary(
            projects,
            projectId => CountOpenTasks(getProjectBoardQueryService.Get(projectId))));
    }

    [HttpGet("projects/{projectId:guid}/board-summary")]
    [Produces("text/plain")]
    public IResult GetProjectBoardSummary(
        Guid projectId,
        [FromServices] IGetProjectBoardQueryService getProjectBoardQueryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = getProjectBoardQueryService.Get(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        if (result.NotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project list summary again and pick an existing project.");
        }

        return PlainText(AiTextContractFormatter.ProjectBoardSummary(result.Resource!));
    }

    [HttpGet("projects/{projectId:guid}/tasks/{taskId:guid}/detail-summary")]
    [Produces("text/plain")]
    public IResult GetTaskDetailSummary(
        Guid projectId,
        Guid taskId,
        [FromServices] GetTaskDetailQueryService getTaskDetailQueryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = getTaskDetailQueryService.Get(currentUserId, projectId, taskId);
        if (result.AccessDenied)
        {
            return ErrorText(StatusCodes.Status403Forbidden, "Forbidden", "Activate a scope you can access or ask the project owner for access.");
        }

        if (result.NotFound)
        {
            return ErrorText(StatusCodes.Status404NotFound, "ResourceNotFound", "Read project board summary again and pick an existing task.");
        }

        return PlainText(AiTextContractFormatter.TaskDetailSummary(result.Resource!));
    }

    private static int CountOpenTasks(ProjectBoardView? board)
    {
        return board?.Columns.Sum(column => column.Tasks.Count) ?? 0;
    }

    private static IResult PlainText(string content)
    {
        return Results.Text(content, "text/plain; charset=utf-8");
    }

    private static IResult ErrorText(int statusCode, string errorCode, string recoveryHint)
    {
        var message = errorCode switch
        {
            "Forbidden" => "Access to the requested resource is denied.",
            "ResourceNotFound" => "The requested resource does not exist.",
            _ => errorCode,
        };

        return Results.Text(
            AiTextContractFormatter.Error(errorCode, message, recoveryHint),
            "text/plain; charset=utf-8",
            Encoding.UTF8,
            statusCode);
    }
}