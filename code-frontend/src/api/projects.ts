import { apiPath, request } from './request'
import type {
  ProjectBoardResponse,
  ProjectInvitationListResponse,
  ProjectInvitationResponse,
  ProjectListResponse,
  ProjectMembersResponse,
  ProjectResponse,
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