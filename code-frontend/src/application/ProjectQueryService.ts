import {
  getInvitationInbox,
  getProjectBoard,
  getProjectCodeTraceability,
  getProjectInvitations,
  getProjectMembers,
  getProjectSubtaskTemplates,
  getProjects,
  type ProjectBoardResponse,
  type ProjectCodeTraceabilityResponse,
  type ProjectInvitationListResponse,
  type ProjectListResponse,
  type ProjectMembersResponse,
  type ProjectSubtaskTemplateListResponse,
} from '../api/ronflowApi'

export class ProjectQueryService {
  async getProjects(): Promise<ProjectListResponse> {
    return getProjects()
  }

  async getBoard(projectId: string): Promise<ProjectBoardResponse> {
    return getProjectBoard(projectId)
  }

  async getCodeTraceability(projectId: string): Promise<ProjectCodeTraceabilityResponse> {
    return getProjectCodeTraceability(projectId)
  }

  async getSubtaskTemplates(projectId: string): Promise<ProjectSubtaskTemplateListResponse> {
    return getProjectSubtaskTemplates(projectId)
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
