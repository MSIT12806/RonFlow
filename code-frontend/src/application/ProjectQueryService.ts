import {
  getCycleTimeReport,
  getInvitationInbox,
  getProjectBoard,
  getProjectCodeTraceability,
  getProjectInvitations,
  getProjectMembers,
  getProjectSubtaskTemplates,
  getTaskAgingReport,
  getWorkflowThroughputReport,
  getProjects,
  type ProjectBoardResponse,
  type CycleTimeReportResponse,
  type ProjectCodeTraceabilityResponse,
  type ProjectInvitationListResponse,
  type ProjectListResponse,
  type ProjectMembersResponse,
  type ProjectSubtaskTemplateListResponse,
  type TaskAgingReportResponse,
  type WorkflowThroughputReportResponse,
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

  async getWorkflowThroughput(projectId: string, bucket: 'day' | 'week'): Promise<WorkflowThroughputReportResponse> {
    return getWorkflowThroughputReport(projectId, bucket)
  }

  async getTaskAging(projectId: string, thresholds: {
    todoThresholdDays: number
    activeThresholdDays: number
    reviewThresholdDays: number
  }): Promise<TaskAgingReportResponse> {
    return getTaskAgingReport(projectId, thresholds)
  }

  async getCycleTime(projectId: string, range: {
    completedFrom: string
    completedTo: string
  }): Promise<CycleTimeReportResponse> {
    return getCycleTimeReport(projectId, range)
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
