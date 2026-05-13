import {
  getArchivedTasks,
  getTaskDetail,
  getTrashedTasks,
  type LifecycleTaskListResponse,
  type TaskDetailResponse,
} from '../api/ronflowApi'

export class TaskQueryService {
  async getDetail(projectId: string, taskId: string): Promise<TaskDetailResponse> {
    return getTaskDetail(projectId, taskId)
  }

  async getArchived(projectId: string): Promise<LifecycleTaskListResponse> {
    return getArchivedTasks(projectId)
  }

  async getTrashed(projectId: string): Promise<LifecycleTaskListResponse> {
    return getTrashedTasks(projectId)
  }
}
