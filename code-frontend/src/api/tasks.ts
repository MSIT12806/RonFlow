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

export async function updateTask(projectId: string, taskId: string, payload: {
  title: string
  description: string
  dueDate: string | null
}) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}`), {
    method: 'PATCH',
    body: JSON.stringify(payload),
  })
}

export async function reorderTask(projectId: string, taskId: string, targetTaskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/order`), {
    method: 'PATCH',
    body: JSON.stringify({ targetTaskId }),
  })
}