import { getTaskDetail, type TaskDetailResponse } from '../api/ronflowApi'

export class TaskQueryService {
  async getDetail(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return getTaskDetail(projectId, taskId)
  }
}
