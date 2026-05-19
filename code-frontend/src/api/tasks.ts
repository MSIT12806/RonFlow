import { apiPath, request } from './request'
import type { LifecycleTaskListResponse, TaskDetailResponse } from './types'

export async function createTask(projectId: string, title: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks`), {
    method: 'POST',
    body: JSON.stringify({ title }),
  })
}

export async function getTaskDetail(projectId: string, taskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}`))
}

export async function getArchivedTasks(projectId: string) {
  return request<LifecycleTaskListResponse>(apiPath(`/projects/${projectId}/tasks/archived`))
}

export async function getTrashedTasks(projectId: string) {
  return request<LifecycleTaskListResponse>(apiPath(`/projects/${projectId}/tasks/trashed`))
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

export async function createTaskReminder(projectId: string, taskId: string, payload: {
  reminderDateTime: string
  description: string
}) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/reminders`), {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export async function deleteTaskReminder(projectId: string, taskId: string, reminderId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/reminders/${reminderId}`), {
    method: 'DELETE',
  })
}

export async function archiveTask(projectId: string, taskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/archive`), {
    method: 'PATCH',
  })
}

export async function restoreArchivedTask(projectId: string, taskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/restore-from-archive`), {
    method: 'PATCH',
  })
}

export async function moveTaskToTrash(projectId: string, taskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/trash`), {
    method: 'PATCH',
  })
}

export async function restoreTrashedTask(projectId: string, taskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/restore-from-trash`), {
    method: 'PATCH',
  })
}