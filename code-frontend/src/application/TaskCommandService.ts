import { changeTaskState, createTask, type TaskDetailResponse, type WorkflowKey } from '../api/ronflowApi'

export class TaskCommandService {
  async create(projectId: string, title: string): Promise<TaskDetailResponse> {
    return createTask(projectId, title)
  }

  async changeState(projectId: string, taskId: string, stateKey: WorkflowKey): Promise<TaskDetailResponse> {
    return changeTaskState(projectId, taskId, stateKey)
  }
}
