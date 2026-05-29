using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;
using RonFlow.Observability;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class ProjectsController : AuthenticatedControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProjectListResponse>(StatusCodes.Status200OK)]
    public IResult GetProjects([FromServices] GetProjectsQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var projects = queryService.Get(currentUserId).Items
            .Select(ProjectListItemResponse.FromView)
            .ToArray();

        return Results.Ok(new ProjectListResponse(projects));
    }

    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IResult CreateProject(
        [FromBody] CreateProjectRequest request,
        [FromServices] CreateProjectCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        if (!TryGetCurrentUserName(out var currentUserName) || !TryGetCurrentUserEmail(out var currentUserEmail))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Create(currentUserId, currentUserName, currentUserEmail, request.Name);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        var project = result.Project!;

        return Results.Created(
            $"/api/projects/{project.Id}/board",
            ProjectResponse.FromOutput(project));
    }

    [HttpGet("{projectId:guid}/board")]
    [ObservedOperation(ObservedOperationNames.BoardRead)]
    [ServiceFilter(typeof(ObservedOperationServerTimingFilter))]
    [ServiceFilter(typeof(ObservedOperationResultTimingFilter))]
    [ProducesResponseType<ProjectBoardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetBoard(
        Guid projectId,
        [FromServices] IGetProjectBoardQueryService queryService,
        [FromServices] ProjectPresenceRegistry projectPresenceRegistry)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.Get(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        if (!result.NotFound
            && TryGetCurrentUserName(out var currentUserName)
            && TryGetRonFlowSessionId(out var sessionId))
        {
            projectPresenceRegistry.EnterProject(currentUserId, currentUserName, sessionId, projectId);
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(ProjectBoardResponse.FromView(result.Resource!));
    }

    [HttpGet("{projectId:guid}/subtask-templates")]
    [ProducesResponseType<ProjectSubtaskTemplateListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetSubtaskTemplates(
        Guid projectId,
        [FromServices] GetProjectSubtaskTemplatesQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.Get(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(ProjectSubtaskTemplateListResponse.FromView(result.Resource!));
    }

    [HttpPut("{projectId:guid}/subtask-templates")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProjectSubtaskTemplateListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult ReplaceSubtaskTemplates(
        Guid projectId,
        [FromBody] ReplaceProjectSubtaskTemplatesRequest request,
        [FromServices] ReplaceProjectSubtaskTemplatesCommandService commandService)
    {
        if (request.Items is null)
        {
            return ValidationResults.FromError(new ValidationError("items", "完成條件清單為必填欄位"));
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Replace(
            currentUserId,
            projectId,
            request.Items.Select(item => new ProjectSubtaskTemplateInput(item.Id, item.Title?.Trim() ?? string.Empty, item.Order)).ToArray());

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.ProjectNotFound
            ? Results.NotFound()
            : Results.Ok(ProjectSubtaskTemplateListResponse.FromOutput(result.Templates!));
    }

    [HttpGet("{projectId:guid}/members")]
    [ProducesResponseType<ProjectMemberListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetMembers(Guid projectId, [FromServices] ProjectCollaborationQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.GetMembers(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(ProjectMemberListResponse.FromView(result.Resource!));
    }

    [HttpGet("{projectId:guid}/invitations")]
    [ProducesResponseType<ProjectInvitationListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetPendingInvitations(Guid projectId, [FromServices] ProjectCollaborationQueryService queryService)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Results.Unauthorized();
        }

        var result = queryService.GetPendingInvitations(currentUserId, projectId);
        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        return result.NotFound
            ? Results.NotFound()
            : Results.Ok(ProjectInvitationListResponse.FromView(result.Resource!));
    }

    [HttpPost("{projectId:guid}/invitations")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProjectInvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult InviteMember(
        Guid projectId,
        [FromBody] CreateProjectInvitationRequest request,
        [FromServices] ProjectInvitationCommandService commandService)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || !TryGetCurrentUserName(out var currentUserName))
        {
            return Results.Unauthorized();
        }

        var result = commandService.Invite(currentUserId, currentUserName, projectId, request.Invitee);

        if (result.ValidationError is not null)
        {
            return ValidationResults.FromError(result.ValidationError);
        }

        if (result.AccessDenied)
        {
            return AccessDenied();
        }

        if (result.ProjectNotFound)
        {
            return Results.NotFound();
        }

        return Results.Created(
            $"/api/projects/{projectId}/invitations/{result.Invitation!.Id}",
            ProjectInvitationResponse.FromView(result.Invitation!));
    }
}
