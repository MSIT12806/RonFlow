import { createProject, type ProjectResponse } from '../api/ronflowApi'

export class ProjectCommandService {
  async create(name: string): Promise<ProjectResponse> {
    return createProject(name)
  }
}
