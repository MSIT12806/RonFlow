import {
  changeTaskState,
  createTask,
  reorderTask,
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
}
