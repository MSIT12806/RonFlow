using RonFlow.Domain;

namespace RonFlow.Application;

public sealed class ProjectAccessService(IProjectRepository projectRepository)
{
    public ProjectAccessResult GetOwnerProject(Guid currentUserId, Guid projectId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return ProjectAccessResult.NotFound();
        }

        if (!project.IsOwnedBy(currentUserId))
        {
            return ProjectAccessResult.Denied();
        }

        return ProjectAccessResult.Success(project);
    }

    public ProjectAccessResult GetOwnedProject(Guid currentUserId, Guid projectId)
    {
        var project = projectRepository.Get(projectId);
        if (project is null)
        {
            return ProjectAccessResult.NotFound();
        }

        if (!project.IsAccessibleBy(currentUserId))
        {
            return ProjectAccessResult.Denied();
        }

        return ProjectAccessResult.Success(project);
    }
}

public sealed record ProjectAccessResult(Project? Project, bool ProjectNotFound, bool AccessDenied)
{
    public static ProjectAccessResult Success(Project project)
    {
        return new(project, false, false);
    }

    public static ProjectAccessResult NotFound()
    {
        return new(null, true, false);
    }

    public static ProjectAccessResult Denied()
    {
        return new(null, false, true);
    }
}