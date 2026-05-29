using RonFlow.Domain;

namespace RonFlow.Application;

public sealed record ProjectSubtaskTemplateInput(Guid? Id, string Title, int? Order);

public sealed record TaskSubtaskInput(Guid? Id, string Title, bool IsChecked, int? Order);

public sealed class GetProjectSubtaskTemplatesQueryService(ProjectAccessService projectAccessService)
{
    public OwnedResourceQueryResult<ProjectSubtaskTemplateListView> Get(Guid currentUserId, Guid projectId)
    {
        var access = projectAccessService.GetOwnerProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return OwnedResourceQueryResult<ProjectSubtaskTemplateListView>.Missing();
        }

        if (access.AccessDenied)
        {
            return OwnedResourceQueryResult<ProjectSubtaskTemplateListView>.Denied();
        }

        var project = access.Project!;
        return OwnedResourceQueryResult<ProjectSubtaskTemplateListView>.Success(
            CoreFlowReadModelFactory.CreateProjectSubtaskTemplates(project.ToModel()));
    }
}

public sealed class ReplaceProjectSubtaskTemplatesCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    TimeProvider timeProvider)
{
    public ReplaceProjectSubtaskTemplatesResult Replace(Guid currentUserId, Guid projectId, IReadOnlyList<ProjectSubtaskTemplateInput> inputs)
    {
        var access = projectAccessService.GetOwnerProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return ReplaceProjectSubtaskTemplatesResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return ReplaceProjectSubtaskTemplatesResult.Denied();
        }

        var normalizedInputs = new List<ProjectSubtaskTemplateInput>(inputs.Count);
        for (var index = 0; index < inputs.Count; index += 1)
        {
            var title = inputs[index].Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return ReplaceProjectSubtaskTemplatesResult.Invalid("items", "完成條件標題為必填欄位");
            }

            normalizedInputs.Add(inputs[index] with
            {
                Title = title,
                Order = inputs[index].Order ?? index,
            });
        }

        var project = access.Project!;
        var changedAt = timeProvider.GetUtcNow();
        project.ReplaceSubtaskTemplates(normalizedInputs.Select(item => (item.Id, item.Title, item.Order ?? 0)), changedAt);
        projectRepository.Update(project);

        return ReplaceProjectSubtaskTemplatesResult.Success(CoreFlowCommandOutputFactory.CreateProjectSubtaskTemplates(project.ToModel()));
    }
}

public sealed class ReplaceTaskSubtasksCommandService(
    IProjectRepository projectRepository,
    ProjectAccessService projectAccessService,
    ITaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public ReplaceTaskSubtasksResult Replace(Guid currentUserId, Guid projectId, Guid taskId, IReadOnlyList<TaskSubtaskInput> inputs)
    {
        var access = projectAccessService.GetOwnedProject(currentUserId, projectId);
        if (access.ProjectNotFound)
        {
            return ReplaceTaskSubtasksResult.NotFound();
        }

        if (access.AccessDenied)
        {
            return ReplaceTaskSubtasksResult.Denied();
        }

        var normalizedInputs = new List<TaskSubtaskInput>(inputs.Count);
        for (var index = 0; index < inputs.Count; index += 1)
        {
            var title = inputs[index].Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return ReplaceTaskSubtasksResult.Invalid("items", "完成條件標題為必填欄位");
            }

            normalizedInputs.Add(inputs[index] with
            {
                Title = title,
                Order = inputs[index].Order ?? index,
            });
        }

        var project = access.Project!;
        var task = taskRepository.Get(taskId);
        if (task is null || task.ProjectId != projectId)
        {
            return ReplaceTaskSubtasksResult.NotFound();
        }

        var reviewState = project.FindWorkflowState("review");
        var changedAt = timeProvider.GetUtcNow();
        task.ReplaceSubtasks(
            normalizedInputs.Select(item => new TaskSubtask(item.Id.GetValueOrDefault(Guid.NewGuid()), item.Title, item.IsChecked, item.Order ?? 0)),
            reviewState,
            changedAt);
        taskRepository.Update(task);
        project.Touch(changedAt);
        projectRepository.Update(project);

        return ReplaceTaskSubtasksResult.Success(CoreFlowCommandOutputFactory.CreateTask(task.ToModel()));
    }
}