import { apiPath, request } from './request'
import type {
  CycleTimeReportResponse,
  ProjectBoardResponse,
  ProjectCodeTraceabilityResponse,
  ProjectInvitationListResponse,
  ProjectInvitationResponse,
  ProjectListResponse,
  ProjectMembersResponse,
  ProjectSubtaskTemplateListResponse,
  ProjectResponse,
  TaskAgingReportResponse,
  WorkflowThroughputReportResponse,
} from './types'

export async function getProjects() {
  return request<ProjectListResponse>(apiPath('/projects'))
}

export async function createProject(name: string) {
  return request<ProjectResponse>(apiPath('/projects'), {
    method: 'POST',
    body: JSON.stringify({ name }),
  })
}

export async function getProjectBoard(projectId: string) {
  return request<ProjectBoardResponse>(apiPath(`/projects/${projectId}/board`))
}

export async function getProjectCodeTraceability(projectId: string) {
  return request<ProjectCodeTraceabilityResponse>(apiPath(`/projects/${projectId}/code-traceability`))
}

export async function getWorkflowThroughputReport(projectId: string, bucket: 'day' | 'week' = 'day') {
  return request<WorkflowThroughputReportResponse>(apiPath(`/projects/${projectId}/reports/workflow-throughput?bucket=${bucket}`))
}

export async function getTaskAgingReport(projectId: string, thresholds: {
  todoThresholdDays: number
  activeThresholdDays: number
  reviewThresholdDays: number
}) {
  const query = new URLSearchParams({
    todoThresholdDays: `${thresholds.todoThresholdDays}`,
    activeThresholdDays: `${thresholds.activeThresholdDays}`,
    reviewThresholdDays: `${thresholds.reviewThresholdDays}`,
  })

  return request<TaskAgingReportResponse>(apiPath(`/projects/${projectId}/reports/task-aging?${query.toString()}`))
}

export async function getCycleTimeReport(projectId: string, range: {
  completedFrom: string
  completedTo: string
}) {
  const query = new URLSearchParams({
    completedFrom: range.completedFrom,
    completedTo: range.completedTo,
  })

  return request<CycleTimeReportResponse>(apiPath(`/projects/${projectId}/reports/cycle-time?${query.toString()}`))
}

export async function getProjectSubtaskTemplates(projectId: string) {
  return request<ProjectSubtaskTemplateListResponse>(apiPath(`/projects/${projectId}/subtask-templates`))
}

export async function replaceProjectSubtaskTemplates(projectId: string, payload: {
  items: Array<{ id?: string | null; title: string; order?: number | null }>
}) {
  return request<ProjectSubtaskTemplateListResponse>(apiPath(`/projects/${projectId}/subtask-templates`), {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export async function getProjectMembers(projectId: string) {
  return request<ProjectMembersResponse>(apiPath(`/projects/${projectId}/members`))
}

export async function getProjectInvitations(projectId: string) {
  return request<ProjectInvitationListResponse>(apiPath(`/projects/${projectId}/invitations`))
}

export async function createProjectInvitation(projectId: string, invitee: string) {
  return request<ProjectInvitationResponse>(apiPath(`/projects/${projectId}/invitations`), {
    method: 'POST',
    body: JSON.stringify({ invitee }),
  })
}

export async function getInvitationInbox() {
  return request<ProjectInvitationListResponse>(apiPath('/invitations'))
}

export async function acceptInvitation(invitationId: string) {
  return request<void>(apiPath(`/invitations/${invitationId}/accept`), {
    method: 'POST',
  })
}

export async function rejectInvitation(invitationId: string) {
  return request<void>(apiPath(`/invitations/${invitationId}/reject`), {
    method: 'POST',
  })
}