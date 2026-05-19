using System.Net.Mime;
using RonFlow.Application;
using RonFlow.Api.Contracts;

namespace RonFlow.Api.Endpoints;

internal static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects", (GetProjectsQueryService queryService) =>
        {
            var projects = queryService.Get().Items
                .Select(ProjectListItemResponse.FromView)
                .ToArray();

            return Results.Ok(new ProjectListResponse(projects));
        });

        app.MapPost("/api/projects", (CreateProjectRequest request, CreateProjectCommandService commandService) =>
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
        })
        .Accepts<CreateProjectRequest>(MediaTypeNames.Application.Json)
        .Produces<ProjectResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/projects/{projectId:guid}/board", (Guid projectId, GetProjectBoardQueryService queryService) =>
        {
            var board = queryService.Get(projectId);
            return board is null ? Results.NotFound() : Results.Ok(ProjectBoardResponse.FromView(board));
        })
        .Produces<ProjectBoardResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}