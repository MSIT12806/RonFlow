import {
  acquireTaskContentEditLock,
  archiveTask,
  changeTaskState,
  createChildTask,
  createTask,
  createTaskReminder,
  deleteTaskReminder,
  duplicateTaskSubtree,
  moveTaskInTree,
  moveTaskToTrash,
  replaceTaskSubtasks,
  reorderTask,
  releaseTaskContentEditLock,
  restoreArchivedTask,
  restoreTrashedTask,
  setTaskSplitComplete,
  updateTask,
  type TaskDetailResponse,
  type TaskEstimatedEffortResponse,
  type WorkflowKey,
} from '../api/ronflowApi'

export class TaskCommandService {
  async create(projectId: string, title: string, isShort = false): Promise<TaskDetailResponse> {
    return createTask(projectId, title, isShort)
  }

  async createChild(projectId: string, parentTaskId: string, title: string): Promise<TaskDetailResponse> {
    return createChildTask(projectId, parentTaskId, title)
  }

  async changeState(projectId: string, taskId: string, stateKey: WorkflowKey): Promise<TaskDetailResponse> {
    return changeTaskState(projectId, taskId, stateKey)
  }

  async update(
    projectId: string,
    taskId: string,
    title: string,
    description: string,
    dueDate: string | null,
    estimatedEffort: TaskEstimatedEffortResponse | null,
    isShort: boolean,
    codeTraceability: {
      api: Array<{ changeType: 'added' | 'modified' | 'removed'; target: string }>
      frontendPages: Array<{ changeType: 'added' | 'modified' | 'removed'; target: string }>
      frontendComponents: Array<{ changeType: 'added' | 'modified' | 'removed'; target: string }>
    },
  ): Promise<TaskDetailResponse> {
    return updateTask(projectId, taskId, { title, description, dueDate, estimatedEffort, isShort, codeTraceability })
  }

  async setSplitComplete(projectId: string, taskId: string, isSplitComplete: boolean): Promise<TaskDetailResponse> {
    return setTaskSplitComplete(projectId, taskId, isSplitComplete)
  }

  async reorder(projectId: string, taskId: string, targetTaskId: string): Promise<TaskDetailResponse> {
    return reorderTask(projectId, taskId, targetTaskId)
  }

  async moveInTree(
    projectId: string,
    taskId: string,
    payload: {
      targetParentTaskId: string | null
      targetSiblingTaskId: string | null
      insertAfter: boolean
    },
  ): Promise<TaskDetailResponse> {
    return moveTaskInTree(projectId, taskId, payload)
  }

  async duplicateSubtree(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return duplicateTaskSubtree(projectId, taskId)
  }

  async replaceSubtasks(
    projectId: string,
    taskId: string,
    items: Array<{ id?: string | null; title: string; isChecked: boolean; order?: number | null }>,
  ): Promise<TaskDetailResponse> {
    return replaceTaskSubtasks(projectId, taskId, { items })
  }

  async acquireContentEditLock(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return acquireTaskContentEditLock(projectId, taskId)
  }

  async releaseContentEditLock(projectId: string, taskId: string): Promise<void> {
    await releaseTaskContentEditLock(projectId, taskId)
  }

  async createReminder(
    projectId: string,
    taskId: string,
    reminderDateTime: string,
    description: string,
  ): Promise<TaskDetailResponse> {
    return createTaskReminder(projectId, taskId, { reminderDateTime, description })
  }

  async deleteReminder(projectId: string, taskId: string, reminderId: string): Promise<TaskDetailResponse> {
    return deleteTaskReminder(projectId, taskId, reminderId)
  }

  async archive(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return archiveTask(projectId, taskId)
  }

  async restoreArchived(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return restoreArchivedTask(projectId, taskId)
  }

  async moveToTrash(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return moveTaskToTrash(projectId, taskId)
  }

  async restoreTrashed(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return restoreTrashedTask(projectId, taskId)
  }
}
