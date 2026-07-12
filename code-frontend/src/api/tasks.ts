import { apiPath, request } from './request'
import type { LifecycleTaskListResponse, TaskDetailResponse, TaskEstimatedEffortResponse } from './types'

export type MoveTaskInTreePayload = {
  targetParentTaskId: string | null
  targetSiblingTaskId: string | null
  insertAfter: boolean
}

export async function createTask(projectId: string, title: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks`), {
    method: 'POST',
    body: JSON.stringify({ title }),
  })
}

export async function createChildTask(projectId: string, parentTaskId: string, title: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${parentTaskId}/children`), {
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
  estimatedEffort: TaskEstimatedEffortResponse | null
  codeTraceability: {
    api: Array<{ changeType: 'added' | 'modified' | 'removed'; target: string }>
    frontendPages: Array<{ changeType: 'added' | 'modified' | 'removed'; target: string }>
    frontendComponents: Array<{ changeType: 'added' | 'modified' | 'removed'; target: string }>
  }
}) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}`), {
    method: 'PATCH',
    body: JSON.stringify(payload),
  })
}

export async function setTaskSplitComplete(projectId: string, taskId: string, isSplitComplete: boolean) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/split-complete`), {
    method: 'PATCH',
    body: JSON.stringify({ isSplitComplete }),
  })
}

export async function replaceTaskSubtasks(projectId: string, taskId: string, payload: {
  items: Array<{ id?: string | null; title: string; isChecked: boolean; order?: number | null }>
}) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/subtasks`), {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export async function reorderTask(projectId: string, taskId: string, targetTaskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/order`), {
    method: 'PATCH',
    body: JSON.stringify({ targetTaskId }),
  })
}

export async function moveTaskInTree(projectId: string, taskId: string, payload: MoveTaskInTreePayload) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/tree-position`), {
    method: 'PATCH',
    body: JSON.stringify(payload),
  })
}

export async function acquireTaskContentEditLock(projectId: string, taskId: string) {
  return request<TaskDetailResponse>(apiPath(`/projects/${projectId}/tasks/${taskId}/content-edit-lock`), {
    method: 'POST',
  })
}

export async function releaseTaskContentEditLock(projectId: string, taskId: string) {
  return request<void>(apiPath(`/projects/${projectId}/tasks/${taskId}/content-edit-lock`), {
    method: 'DELETE',
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
