import { apiPath, request } from './request'
import type { ProjectBoardResponse, ProjectListResponse, ProjectResponse } from './types'

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