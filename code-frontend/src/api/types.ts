export type WorkflowKey = string

export type WorkflowStateResponse = {
  key: WorkflowKey
  label: string
  isInitialState: boolean
  isCompletedState: boolean
}

export type ActivityTimelineItemResponse = {
  type: string
  message: string
  occurredAt: string
}

export type TaskReminderResponse = {
  id: string
  reminderDateTime: string
  description: string
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
  reminders?: TaskReminderResponse[]
  activityTimeline: ActivityTimelineItemResponse[]
}

export type TaskLifecycleState = 'activeRecord' | 'archived' | 'trashed'

export type LifecycleTaskListItemResponse = {
  id: string
  projectId: string
  projectName: string
  title: string
  originalState: WorkflowStateResponse
  changedAt: string
}

export type LifecycleTaskListResponse = {
  items: LifecycleTaskListItemResponse[]
}

export type BoardTaskCardResponse = {
  id: string
  title: string
}

export type BoardColumnResponse = {
  stateKey: WorkflowKey
  label: string
  isInitialState: boolean
  isCompletedState: boolean
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
  role?: string
}

export type ProjectMemberResponse = {
  userName: string
  role: string
}

export type ProjectMembersResponse = {
  items: ProjectMemberResponse[]
}

export type ProjectInvitationResponse = {
  id: string
  invitee: string
  projectName?: string
  inviterName?: string
}

export type ProjectInvitationListResponse = {
  items: ProjectInvitationResponse[]
}

export type ProjectListResponse = {
  items: ProjectListItemResponse[]
}

export type ValidationErrorBag = Record<string, string[]>

export type PushNotificationPublicKeyResponse = {
  publicKey: string
}