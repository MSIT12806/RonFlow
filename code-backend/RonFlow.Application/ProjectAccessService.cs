using RonFlow.Domain;

namespace RonFlow.Application;

/// <summary>
/// 封裝專案存在性與權限檢查。
/// </summary>
public sealed class ProjectAccessService(IProjectRepository projectRepository)
{
    /// <summary>
    /// 取得只有專案擁有者可操作的專案實體。
    /// </summary>
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

    /// <summary>
    /// 取得專案成員可存取的專案實體。
    /// </summary>
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

/// <summary>
/// 表示專案查找與權限檢查的結果。
/// </summary>
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