import {
  getInvitationInbox,
  getProjectBoard,
  getProjectInvitations,
  getProjectMembers,
  getProjects,
  type ProjectBoardResponse,
  type ProjectInvitationListResponse,
  type ProjectListResponse,
  type ProjectMembersResponse,
} from '../api/ronflowApi'

export class ProjectQueryService {
  async getProjects(): Promise<ProjectListResponse> {
    return getProjects()
  }

  async getBoard(projectId: string): Promise<ProjectBoardResponse> {
    return getProjectBoard(projectId)
  }

  async getMembers(projectId: string): Promise<ProjectMembersResponse> {
    return getProjectMembers(projectId)
  }

  async getPendingInvitations(projectId: string): Promise<ProjectInvitationListResponse> {
    return getProjectInvitations(projectId)
  }

  async getInvitationInbox(): Promise<ProjectInvitationListResponse> {
    return getInvitationInbox()
  }
}
