import {
  acceptInvitation,
  createProject,
  createProjectInvitation,
  rejectInvitation,
  type ProjectInvitationResponse,
  type ProjectResponse,
} from '../api/ronflowApi'

export class ProjectCommandService {
  async create(name: string): Promise<ProjectResponse> {
    return createProject(name)
  }

  async inviteMember(projectId: string, invitee: string): Promise<ProjectInvitationResponse> {
    return createProjectInvitation(projectId, invitee)
  }

  async acceptInvitation(invitationId: string): Promise<void> {
    await acceptInvitation(invitationId)
  }

  async rejectInvitation(invitationId: string): Promise<void> {
    await rejectInvitation(invitationId)
  }
}
