using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProjectListResponse>(StatusCodes.Status200OK)]
    public IResult GetProjects([FromServices] GetProjectsQueryService queryService)
    {
        var projects = queryService.Get().Items
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
        var result = commandService.Create(request.Name);

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
    [ProducesResponseType<ProjectBoardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult GetBoard(Guid projectId, [FromServices] GetProjectBoardQueryService queryService)
    {
        var board = queryService.Get(projectId);
        return board is null ? Results.NotFound() : Results.Ok(ProjectBoardResponse.FromView(board));
    }
}