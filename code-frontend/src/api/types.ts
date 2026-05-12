export type WorkflowKey = 'todo' | 'active' | 'review' | 'done'

export type WorkflowStateResponse = {
  key: WorkflowKey
  label: string
  isInitialState: boolean
}

export type ActivityTimelineItemResponse = {
  type: string
  message: string
  occurredAt: string
}

export type TaskDetailResponse = {
  id: string
  projectId: string
  title: string
  description: string
  currentState: WorkflowStateResponse
  dueDate: string | null
  createdAt: string
  completedAt: string | null
  activityTimeline: ActivityTimelineItemResponse[]
}

export type BoardTaskCardResponse = {
  id: string
  title: string
}

export type BoardColumnResponse = {
  stateKey: WorkflowKey
  label: string
  isInitialState: boolean
  emptyStateMessage: string
  tasks: BoardTaskCardResponse[]
}

export type ProjectBoardResponse = {
  projectId: string
  projectName: string
  columns: BoardColumnResponse[]
}

export type ProjectResponse = {
  id: string
  name: string
  updatedAt: string
  workflowStates: WorkflowStateResponse[]
}

export type ProjectListItemResponse = {
  id: string
  name: string
  updatedAt: string
}

export type ProjectListResponse = {
  items: ProjectListItemResponse[]
}

export type ValidationErrorBag = Record<string, string[]>