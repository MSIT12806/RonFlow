import {
  acceptInvitation,
  createProject,
  createProjectInvitation,
  replaceProjectSubtaskTemplates,
  rejectInvitation,
  type ProjectInvitationResponse,
  type ProjectResponse,
  type ProjectSubtaskTemplateListResponse,
} from '../api/ronflowApi'

export class ProjectCommandService {
  async create(name: string): Promise<ProjectResponse> {
    return createProject(name)
  }

  async inviteMember(projectId: string, invitee: string): Promise<ProjectInvitationResponse> {
    return createProjectInvitation(projectId, invitee)
  }

  async replaceSubtaskTemplates(
    projectId: string,
    items: Array<{ id?: string | null; title: string; order?: number | null }>,
  ): Promise<ProjectSubtaskTemplateListResponse> {
    return replaceProjectSubtaskTemplates(projectId, { items })
  }

  async acceptInvitation(invitationId: string): Promise<void> {
    await acceptInvitation(invitationId)
  }

  async rejectInvitation(invitationId: string): Promise<void> {
    await rejectInvitation(invitationId)
  }
}
