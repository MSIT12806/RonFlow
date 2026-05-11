import { getProjectBoard, getProjects, type ProjectBoardResponse, type ProjectListResponse } from '../api/ronflowApi'

export class ProjectQueryService {
  async getProjects(): Promise<ProjectListResponse> {
    return getProjects()
  }

  async getBoard(projectId: string): Promise<ProjectBoardResponse> {
    return getProjectBoard(projectId)
  }
}
