import {
  acquireTaskContentEditLock,
  archiveTask,
  changeTaskState,
  createTask,
  createTaskReminder,
  deleteTaskReminder,
  moveTaskToTrash,
  replaceTaskSubtasks,
  reorderTask,
  releaseTaskContentEditLock,
  restoreArchivedTask,
  restoreTrashedTask,
  updateTask,
  type TaskDetailResponse,
  type WorkflowKey,
} from '../api/ronflowApi'

export class TaskCommandService {
  async create(projectId: string, title: string): Promise<TaskDetailResponse> {
    return createTask(projectId, title)
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
  ): Promise<TaskDetailResponse> {
    return updateTask(projectId, taskId, { title, description, dueDate })
  }

  async reorder(projectId: string, taskId: string, targetTaskId: string): Promise<TaskDetailResponse> {
    return reorderTask(projectId, taskId, targetTaskId)
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
