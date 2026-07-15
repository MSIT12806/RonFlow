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

export type TaskCodeTraceabilityChangeType = 'added' | 'modified' | 'removed'

export type TaskCodeTraceabilityItemResponse = {
  changeType: TaskCodeTraceabilityChangeType
  target: string
}

export type TaskCodeTraceabilityResponse = {
  api: TaskCodeTraceabilityItemResponse[]
  frontendPages: TaskCodeTraceabilityItemResponse[]
  frontendComponents: TaskCodeTraceabilityItemResponse[]
}

export type TaskReminderResponse = {
  id: string
  reminderDateTime: string
  description: string
}

export type TaskEstimatedEffortUnit = 'minutes' | 'hours' | 'days'

export type TaskEstimatedEffortResponse = {
  value: number
  unit: TaskEstimatedEffortUnit
}

export type ProjectSubtaskTemplateResponse = {
  id: string
  title: string
  order: number
}

export type ProjectSubtaskTemplateListResponse = {
  items: ProjectSubtaskTemplateResponse[]
}

export type TaskSubtaskResponse = {
  id: string
  title: string
  isChecked: boolean
  order: number
}

export type TaskDetailResponse = {
  id: string
  projectId: string
  parentTaskId: string | null
  title: string
  description: string
  currentState: WorkflowStateResponse
  isInFlow: boolean
  isSplitComplete: boolean
  isShort: boolean
  lifecycleState: TaskLifecycleState
  dueDate: string | null
  createdAt: string
  completedAt: string | null
  estimatedEffort: TaskEstimatedEffortResponse | null
  subtasks: TaskSubtaskResponse[]
  childTasks: BoardTaskCardResponse[]
  parentPath: string
  codeTraceability: TaskCodeTraceabilityResponse
  reminders?: TaskReminderResponse[]
  activityTimeline: ActivityTimelineItemResponse[]
  canEnterEdit: boolean
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
  isCompleted: boolean
  isInFlow: boolean
  isSplitComplete: boolean
  createdAt: string
  completedAt: string | null
  parentPath: string
  children: BoardTaskCardResponse[]
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
  taskTree: BoardTaskCardResponse[]
  columns: BoardColumnResponse[]
}

export type ProjectCodeTraceabilityCategory = 'api' | 'frontendPages' | 'frontendComponents'

export type ProjectCodeTraceabilityItemResponse = {
  taskId: string
  taskTitle: string
  category: ProjectCodeTraceabilityCategory
  changeType: TaskCodeTraceabilityChangeType
  target: string
}

export type ProjectCodeTraceabilityResponse = {
  items: ProjectCodeTraceabilityItemResponse[]
}

export type WorkflowThroughputBucketResponse = {
  bucketStart: string
  createdCount: number
  movedToActiveCount: number
  movedToReviewCount: number
  completedCount: number
  reopenedCount: number
}

export type WorkflowThroughputReportResponse = {
  projectId: string
  bucketType: 'day' | 'week'
  lastUpdatedAt: string | null
  buckets: WorkflowThroughputBucketResponse[]
}

export type TaskAgingStateThresholdResponse = {
  stateKey: WorkflowKey
  stateLabel: string
  thresholdDays: number
}

export type TaskAgingTaskItemResponse = {
  taskId: string
  title: string
  currentState: WorkflowStateResponse
  enteredStateAt: string
  agingDays: number
}

export type TaskAgingReportResponse = {
  projectId: string
  lastUpdatedAt: string
  thresholds: TaskAgingStateThresholdResponse[]
  items: TaskAgingTaskItemResponse[]
}

export type CycleTimeMetricSummaryResponse = {
  sampleCount: number
  averageHours: number | null
  medianHours: number | null
  p90Hours: number | null
}

export type CycleTimeStateTransitionSummaryResponse = {
  fromStateKey: WorkflowKey
  fromStateLabel: string
  toStateKey: WorkflowKey
  toStateLabel: string
  duration: CycleTimeMetricSummaryResponse
}

export type CycleTimeReportResponse = {
  projectId: string
  completedFrom: string
  completedTo: string
  lastUpdatedAt: string
  leadTime: CycleTimeMetricSummaryResponse
  cycleTime: CycleTimeMetricSummaryResponse
  stateTransitions: CycleTimeStateTransitionSummaryResponse[]
}

export type CompletedTasksByMonthTaskResponse = {
  taskId: string
  title: string
  completedAt: string
}

export type CompletedTasksByMonthBucketResponse = {
  monthStart: string
  tasks: CompletedTasksByMonthTaskResponse[]
}

export type CompletedTasksByMonthReportResponse = {
  projectId: string
  anchorMonth: string
  monthCount: number
  lastUpdatedAt: string
  canMoveNewer: boolean
  canMoveOlder: boolean
  months: CompletedTasksByMonthBucketResponse[]
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
  activeTasks: ProjectActiveTaskResponse[]
}

export type ProjectActiveTaskResponse = {
  id: string
  title: string
}

export type ProjectMemberResponse = {
  userName: string
  role: string
}

export type ProjectOnlineUserResponse = {
  userName: string
}

export type ProjectMembersResponse = {
  items: ProjectMemberResponse[]
  onlineUsers: ProjectOnlineUserResponse[]
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

export type DatabaseSyncOperationStatus = 'queued' | 'running' | 'succeeded' | 'failed'

export type DatabaseSyncOperationResponse = {
  id: string
  reason: string
  status: DatabaseSyncOperationStatus
  requestedAt: string
  startedAt: string | null
  completedAt: string | null
  failureSummary: string | null
}

export type DatabaseSyncOperationListResponse = {
  items: DatabaseSyncOperationResponse[]
}
