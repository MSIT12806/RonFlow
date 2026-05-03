import { apiPath, request } from './request'
import type { TaskDetailResponse } from './types'

export async function createTask(projectId: string, title: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks`), {
    method: 'POST',
    body: JSON.stringify({ title }),
  })
}

export async function getTaskDetail(projectId: string, taskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}`))
}

export async function changeTaskState(projectId: string, taskId: string, stateKey: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/state`), {
    method: 'PATCH',
    body: JSON.stringify({ stateKey }),
  })
}