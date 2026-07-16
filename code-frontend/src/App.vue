

<template>
  <AsyncStatePlayground v-if="showAsyncStatePlayground" />

  <main v-else class="app-shell">
    <Toast group="database-sync" position="bottom-right" />
    <Toast position="bottom-right" />

    <div class="ambient ambient-left"></div>
    <div class="ambient ambient-right"></div>

    <section v-if="isAuthenticated" class="workspace-shell">
      <header class="topbar">
        <div>
          <p class="eyebrow">RonFlow</p>
          <h1 class="app-title">專案流程看板</h1>
          <p class="app-subtitle">以真後端 API 驅動專案看板，讓使用者流程與後端規則保持一致。</p>
        </div>

        <div class="topbar-actions">
          <div class="user-chip">
            <span class="user-chip-label">目前使用者</span>
            <strong>{{ currentUser?.userName }}</strong>
            <span>{{ currentUser?.email }}</span>
          </div>

          <div class="sync-notification-menu">
            <button
              type="button"
              class="secondary-button sync-notification-button"
              title="Git 同步通知"
              @click="toggleDatabaseSyncNotifications"
            >
              <i class="pi pi-cloud-upload" aria-hidden="true"></i>
              <span>同步</span>
              <span
                v-if="databaseSyncUnreadCount > 0"
                class="sync-notification-badge"
              >
                {{ databaseSyncUnreadCount }}
              </span>
            </button>

            <section
              v-if="isDatabaseSyncNotificationsOpen"
              class="sync-notification-panel"
              aria-label="Git 同步通知"
            >
              <div class="sync-notification-panel-header">
                <div>
                  <p class="eyebrow">Git sync</p>
                  <h2>同步通知</h2>
                </div>
                <span
                  class="sync-notification-connection"
                  :class="{ 'sync-notification-connection-online': isDatabaseSyncNotificationConnected }"
                >
                  {{ isDatabaseSyncNotificationConnected ? '已連線' : '未連線' }}
                </span>
              </div>

              <p v-if="databaseSyncNotificationError" class="sync-notification-error">
                {{ databaseSyncNotificationError }}
              </p>

              <p v-else-if="databaseSyncNotifications.length === 0" class="sync-notification-empty">
                目前沒有 Git 同步結果。
              </p>

              <ul v-else class="sync-notification-list">
                <li
                  v-for="operation in databaseSyncNotifications"
                  :key="operation.id"
                  class="sync-notification-item"
                >
                  <div class="sync-notification-item-header">
                    <span
                      class="sync-notification-status"
                      :class="`sync-notification-status-${operation.status}`"
                    >
                      {{ formatDatabaseSyncStatus(operation.status) }}
                    </span>
                    <time>{{ formatDatabaseSyncTime(operation) }}</time>
                  </div>
                  <p>{{ operation.reason }}</p>
                  <small v-if="operation.failureSummary">{{ operation.failureSummary }}</small>
                </li>
              </ul>
            </section>
          </div>

          <button type="button" class="secondary-button" @click="onRefreshCurrentUser">
            重新整理 me
          </button>

          <button type="button" class="primary-button" @click="createProjectModalRef?.open()">
            建立專案
          </button>

          <button type="button" class="ghost-button" @click="onLogout">
            登出
          </button>
        </div>
      </header>

      <AsyncStateBoundary
        :is-loading="false"
        :error-message="pageError"
        error-scope="page"
      >
        <section
          class="workspace-layout"
          :class="{ 'workspace-layout-collapsed': isTaskDetailOpen && taskDetailViewMode === 'drawer' }"
          data-testid="workspace-layout"
        >
          <ProjectSidebar
            :projects="projects"
            :active-project-id="activeProjectId"
            :invitation-inbox-count="invitationInboxCount"
            :is-loading-projects="isLoadingProjects"
            :has-error="Boolean(pageError)"
            :format-project-meta="formatProjectMeta"
            :completed-tasks-visibility="completedTasksVisibility"
            @select-project="onSelectProject"
            @open-invitation-inbox="openInvitationInbox"
            @change-completed-tasks-visibility="onChangeCompletedTasksVisibility"
          />

          <ProjectBoard
            v-if="currentWorkspaceView === 'board'"
            :active-project-name="activeProject?.name ?? null"
            :task-tree="activeTaskTree"
            :columns="displayColumns"
            :is-loading-board="isLoadingBoard"
            :command-error-message="boardCommandError"
            :can-manage-members="activeProject?.role !== '專案成員'"
            :completed-column-summaries="completedColumnSummaries"
            @open-create-task="onOpenCreateTask"
            @open-project-subtask-templates="openProjectSubtaskTemplatesModal"
            @open-project-members="openProjectMembersPanel"
            @open-archived-tasks="openArchivedTasksView"
            @open-code-traceability="openCodeTraceabilityView"
            @open-reports="openReportsView"
            @open-trash-view="openTrashView"
            @open-task-detail="onOpenTaskDetail"
            @move-task-to-trash="onMoveTaskToTrash"
            @duplicate-task-subtree="onDuplicateTaskSubtree"
            @move-task-to-state="onMoveTaskToState"
            @reorder-task-within-column="reorderTaskWithinColumn"
            @move-task-within-tree="moveTaskWithinTree"
          />

          <ProjectMembersPanel
            v-else-if="currentWorkspaceView === 'members'"
            :active-project-id="activeProjectId"
            :active-project-name="activeProject?.name ?? null"
            :current-user-name="currentUser?.userName ?? null"
            @back-to-board="openBoardView"
          />

          <InvitationInboxView
            v-else-if="currentWorkspaceView === 'invitations'"
            @invitation-accepted="onInvitationAccepted"
            @invitations-changed="onInvitationsChanged"
            @back-to-board="openBoardView"
          />

          <LifecycleTaskListView
            v-else-if="currentWorkspaceView === 'archived'"
            title="已封存任務"
            empty-message="目前沒有已封存任務"
            description="封存後的任務會顯示在這裡，之後可從這裡查看與還原。"
            :items="archivedTasks"
            :is-loading="isLoadingArchivedTasks"
            :error-message="archivedTasksError"
            loading-message="正在載入已封存任務..."
            time-label="封存時間"
            :format-timeline-time="formatTimelineTime"
            @back-to-board="openBoardView"
            @open-task-detail="(taskId, taskTitle) => onOpenLifecycleTaskDetail(taskId, 'archived', taskTitle)"
            @restore-task="onRestoreTask($event, 'archived')"
          />

          <LifecycleTaskListView
            v-else-if="currentWorkspaceView === 'trash'"
            title="垃圾桶"
            empty-message="垃圾桶目前沒有任務"
            description="移到垃圾桶的任務會顯示在這裡，之後可從這裡查看與還原。"
            :items="trashedTasks"
            :is-loading="isLoadingTrashedTasks"
            :error-message="trashedTasksError"
            loading-message="正在載入垃圾桶任務..."
            time-label="移到垃圾桶時間"
            :format-timeline-time="formatTimelineTime"
            @back-to-board="openBoardView"
            @open-task-detail="(taskId, taskTitle) => onOpenLifecycleTaskDetail(taskId, 'trashed', taskTitle)"
            @restore-task="onRestoreTask($event, 'trashed')"
          />

          <CodeTraceabilityQueryView
            v-else-if="currentWorkspaceView === 'codeTraceability'"
            :items="codeTraceabilityItems"
            :is-loading="isLoadingCodeTraceability"
            :error-message="codeTraceabilityError"
            @back-to-board="openBoardView"
            @open-task-detail="onOpenCodeTraceabilityTaskDetail"
          />

          <ProjectReportsView
            v-else-if="currentWorkspaceView === 'reports'"
            :active-project-name="activeProject?.name ?? null"
            :report="workflowThroughputReport"
            :aging-report="taskAgingReport"
            :cycle-report="cycleTimeReport"
            :completed-by-month-report="completedTasksByMonthReport"
            :aging-thresholds="taskAgingThresholds"
            :cycle-range="cycleTimeRange"
            :bucket-type="workflowThroughputBucket"
            :is-loading="isLoadingWorkflowThroughput"
            :is-loading-aging="isLoadingTaskAging"
            :is-loading-cycle="isLoadingCycleTime"
            :is-loading-completed-by-month="isLoadingCompletedTasksByMonth"
            :error-message="workflowThroughputError"
            :aging-error-message="taskAgingError"
            :cycle-error-message="cycleTimeError"
            :completed-by-month-error-message="completedTasksByMonthError"
            @back-to-board="openBoardView"
            @change-bucket="loadWorkflowThroughputReport"
            @change-task-aging-thresholds="loadTaskAgingReport"
            @change-cycle-range="loadCycleTimeReport"
            @shift-completed-month-window="shiftCompletedMonthWindow"
            @open-task-detail="onOpenTaskDetail"
          />
        </section>
      </AsyncStateBoundary>
    </section>

    <CreateProjectModal
      v-if="isAuthenticated"
      ref="createProjectModalRef"
      @project-created="onProjectCreated"
    />

    <CreateTaskModal
      v-if="isAuthenticated"
      ref="createTaskModalRef"
      @task-created="onTaskCreated"
    />

    <TaskDetailModal
      v-if="isAuthenticated"
      :is-open="isTaskDetailOpen"
      :is-loading="isLoadingTaskDetail"
      :is-saving="isUpdatingTaskDetail"
      :is-editing="isEditingTaskDetail"
      :is-read-only="isTaskDetailReadOnly"
      :can-enter-edit="selectedTask?.canEnterEdit ?? true"
      :error-message="taskDetailError"
      :save-error-message="taskDetailCommandError"
      :title-validation-error="taskTitleValidationError"
      :reminder-datetime-validation-error="reminderDateTimeValidationError"
      :reminder-delivery-status-message="reminderDeliveryStatusMessage"
      :can-enable-reminder-delivery="canEnableReminderDelivery"
      :is-enabling-reminder-delivery="isEnablingReminderDelivery"
      :mode="taskDetailMode"
      :view-mode="taskDetailViewMode"
      :display-title="taskDetailDisplayTitle"
      :task="selectedTask"
      :format-timeline-time="formatTimelineTime"
      @close="closeTaskDetail"
      @enter-edit="enterTaskDetailEditMode"
      @save="onTaskDetailSave"
      @send-to-flow="onSendTaskToFlow"
      @replace-subtasks="onReplaceTaskSubtasks"
      @set-split-complete="onSetTaskSplitComplete"
      @create-child-task="onCreateChildTask"
      @open-child-task="onOpenTaskDetail"
      @add-reminder="onAddReminder"
      @delete-reminder="onDeleteReminder"
      @enable-reminder-delivery="enableReminderDelivery"
      @archive="onArchiveTask"
      @move-to-trash="onMoveTaskToTrash"
      @restore="onRestoreTask"
    />

    <ProjectSubtaskTemplatesModal
      v-if="isAuthenticated"
      :is-open="isProjectSubtaskTemplatesOpen"
      :is-loading="isLoadingProjectSubtaskTemplates"
      :is-saving="isSavingProjectSubtaskTemplates"
      :error-message="projectSubtaskTemplatesError"
      :save-error-message="projectSubtaskTemplatesCommandError"
      :project-name="activeProject?.name ?? null"
      :items="projectSubtaskTemplates"
      @close="closeProjectSubtaskTemplatesModal"
      @save="onSaveProjectSubtaskTemplates"
    />

    <section v-if="!isAuthenticated" class="workspace-shell auth-shell">
      <RonAuthEntryPanel
        :is-initializing="isInitializingAuth"
        :is-submitting="isSubmittingAuth"
        :error-message="authErrorMessage"
        :validation-errors="authValidationErrors"
        @login="onLogin"
        @register="onRegister"
      />
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import Toast from 'primevue/toast'
import { useToast } from 'primevue/usetoast'
import AsyncStatePlayground from './devtools/playground/AsyncStatePlayground.vue'
import AsyncStateBoundary from './components/bases/AsyncStateBoundary.vue'
import CodeTraceabilityQueryView from './components/CodeTraceabilityQueryView.vue'
import CreateProjectModal from './components/CreateProjectModal.vue'
import CreateTaskModal from './components/CreateTaskModal.vue'
import InvitationInboxView from './components/InvitationInboxView.vue'
import LifecycleTaskListView from './components/LifecycleTaskListView.vue'
import ProjectBoard from './components/ProjectBoard.vue'
import ProjectMembersPanel from './components/ProjectMembersPanel.vue'
import ProjectReportsView from './components/ProjectReportsView.vue'
import ProjectSidebar from './components/ProjectSidebar.vue'
import ProjectSubtaskTemplatesModal from './components/ProjectSubtaskTemplatesModal.vue'
import RonAuthEntryPanel from './components/RonAuthEntryPanel.vue'
import TaskDetailModal from './components/TaskDetailModal.vue'
import type { PasswordLoginInput, RegisterUserInput } from './api/ronauth'
import type {
  BoardColumnResponse,
  CompletedTasksByMonthReportResponse,
  CycleTimeReportResponse,
  DatabaseSyncOperationResponse,
  DatabaseSyncOperationStatus,
  TaskNotificationResponse,
  ProjectCodeTraceabilityItemResponse,
  ProjectSubtaskTemplateResponse,
  TaskAgingReportResponse,
  TaskEstimatedEffortResponse,
  WorkflowKey,
  WorkflowThroughputReportResponse,
} from './api/ronflowApi'
import { ApiValidationError, activateRonFlowSession, releaseRonFlowProjectScope } from './api/ronflowApi'
import { ProjectCommandService, ProjectQueryService } from './application'
import { useDatabaseSyncNotifications } from './composables/useDatabaseSyncNotifications'
import { usePushNotifications } from './composables/usePushNotifications'
import { useRonFlowAuth } from './composables/useRonFlowAuth'
import {
  COMPLETED_TASKS_VISIBILITY_STORAGE_KEY,
  getCompletedTasksVisibilityLabel,
  isCompletedTaskVisible,
  isCompletedTasksVisibilityValue,
  type CompletedTasksVisibilityValue,
} from './features/completedTasksVisibility'
import {
  useRonFlowBoard,
  type EditableTaskCodeTraceability,
  type EditableTaskSubtask,
  type TaskDetailMode,
} from './composables/useRonFlowBoard'
import { onRonFlowSessionInvalidated } from './ronflowSession'

type WorkspaceView = 'board' | 'members' | 'invitations' | 'archived' | 'trash' | 'codeTraceability' | 'reports'

function formatDateInput(value: Date) {
  return value.toISOString().slice(0, 10)
}

function createDefaultCycleTimeRange() {
  const completedTo = new Date()
  const completedFrom = new Date(completedTo)
  completedFrom.setDate(completedFrom.getDate() - 29)

  return {
    completedFrom: formatDateInput(completedFrom),
    completedTo: formatDateInput(completedTo),
  }
}

function createCurrentMonthAnchor() {
  const now = new Date()
  return `${now.getUTCFullYear()}-${`${now.getUTCMonth() + 1}`.padStart(2, '0')}-01`
}

function addMonthsToIsoDate(value: string, monthDelta: number) {
  const date = new Date(`${value}T00:00:00Z`)
  date.setUTCMonth(date.getUTCMonth() + monthDelta)
  return `${date.getUTCFullYear()}-${`${date.getUTCMonth() + 1}`.padStart(2, '0')}-01`
}

function getInitialCompletedTasksVisibility(): CompletedTasksVisibilityValue {
  if (typeof window === 'undefined') {
    return 'current-month'
  }

  const storedValue = window.localStorage.getItem(COMPLETED_TASKS_VISIBILITY_STORAGE_KEY)
  return storedValue && isCompletedTasksVisibilityValue(storedValue)
    ? storedValue
    : 'current-month'
}

const showAsyncStatePlayground = import.meta.env.DEV
  && new URLSearchParams(window.location.search).get('playground') === 'async-states'

const createProjectModalRef = ref<InstanceType<typeof CreateProjectModal> | null>(null)
const createTaskModalRef = ref<InstanceType<typeof CreateTaskModal> | null>(null)
const currentWorkspaceView = ref<WorkspaceView>('board')
const taskDetailViewMode = ref<'drawer' | 'modal'>('drawer')
const invitationInboxCount = ref(0)
const isProjectSubtaskTemplatesOpen = ref(false)
const isLoadingProjectSubtaskTemplates = ref(false)
const isSavingProjectSubtaskTemplates = ref(false)
const projectSubtaskTemplates = ref<ProjectSubtaskTemplateResponse[]>([])
const projectSubtaskTemplatesError = ref('')
const projectSubtaskTemplatesCommandError = ref('')
const codeTraceabilityItems = ref<ProjectCodeTraceabilityItemResponse[]>([])
const isLoadingCodeTraceability = ref(false)
const codeTraceabilityError = ref('')
const workflowThroughputReport = ref<WorkflowThroughputReportResponse | null>(null)
const workflowThroughputBucket = ref<'day' | 'week'>('day')
const isLoadingWorkflowThroughput = ref(false)
const workflowThroughputError = ref('')
const taskAgingReport = ref<TaskAgingReportResponse | null>(null)
const taskAgingThresholds = ref({
  todoThresholdDays: 7,
  activeThresholdDays: 3,
  reviewThresholdDays: 2,
})
const isLoadingTaskAging = ref(false)
const taskAgingError = ref('')
const cycleTimeReport = ref<CycleTimeReportResponse | null>(null)
const cycleTimeRange = ref(createDefaultCycleTimeRange())
const isLoadingCycleTime = ref(false)
const cycleTimeError = ref('')
const completedTasksByMonthReport = ref<CompletedTasksByMonthReportResponse | null>(null)
const completedTasksByMonthWindow = ref({
  anchorMonth: createCurrentMonthAnchor(),
  monthCount: 3,
})
const isLoadingCompletedTasksByMonth = ref(false)
const completedTasksByMonthError = ref('')
const completedTasksVisibility = ref<CompletedTasksVisibilityValue>(getInitialCompletedTasksVisibility())
const isDatabaseSyncNotificationsOpen = ref(false)

const projectQueryService = new ProjectQueryService()
const projectCommandService = new ProjectCommandService()
const toast = useToast()
let workspacePollTimer: ReturnType<typeof window.setInterval> | null = null
let isPollingWorkspace = false
let removeSessionInvalidatedListener: (() => void) | null = null

const {
  user: currentUser,
  isAuthenticated,
  isInitializing: isInitializingAuth,
  isSubmitting: isSubmittingAuth,
  errorMessage: authErrorMessage,
  validationErrors: authValidationErrors,
  initialize,
  login,
  register,
  loadCurrentUser,
  logout,
  clearLocalSession,
} = useRonFlowAuth()

const {
  reminderDeliveryStatusMessage,
  canEnableReminderDelivery,
  isEnablingReminderDelivery,
  enableReminderDelivery,
} = usePushNotifications()

const {
  operations: databaseSyncNotifications,
  unreadCount: databaseSyncUnreadCount,
  isConnected: isDatabaseSyncNotificationConnected,
  errorMessage: databaseSyncNotificationError,
  start: startDatabaseSyncNotifications,
  stop: stopDatabaseSyncNotifications,
  markAllSeen: markDatabaseSyncNotificationsSeen,
} = useDatabaseSyncNotifications({
  onCompletedOperation: showDatabaseSyncToast,
  onTaskNotification: showTaskNotificationToast,
})

const {
  projects,
  activeProjectId,
  activeProject,
  activeColumns,
  activeTaskTree,
  selectedTask,
  taskDetailDisplayTitle,
  taskDetailMode,
  isTaskDetailReadOnly,
  isEditingTaskDetail,
  archivedTasks,
  trashedTasks,
  isTaskDetailOpen,
  isLoadingProjects,
  isLoadingBoard,
  isLoadingTaskDetail,
  isUpdatingTaskDetail,
  isLoadingArchivedTasks,
  isLoadingTrashedTasks,
  taskDetailError,
  taskDetailCommandError,
  archivedTasksError,
  trashedTasksError,
  pageError,
  boardCommandError,
  taskTitleValidationError,
  reminderDateTimeValidationError,
  openTaskDetail,
  enterTaskDetailEditMode,
  selectProject,
  closeTaskDetail,
  moveTaskToState,
  updateTaskDetail,
  sendTaskToFlow,
  replaceTaskSubtasks,
  setTaskSplitComplete,
  createChildTask,
  createReminder,
  deleteReminder,
  reorderTaskWithinColumn,
  moveTaskWithinTree,
  duplicateTaskSubtree,
  loadArchivedTasks,
  loadTrashedTasks,
  archiveTask,
  moveTaskIntoTrash,
  restoreArchivedTask,
  restoreTrashedTask,
  formatProjectMeta,
  formatTimelineTime,
  loadProjects,
  refreshProjectsSilently,
  loadBoard,
  refreshBoardSilently,
  refreshSelectedTaskDetailSilently,
} = useRonFlowBoard()

type DisplayBoardColumn = BoardColumnResponse
type CompletedColumnSummary = {
  stateKey: string
  selectedLabel: string
  hiddenTaskCount: number
}

const displayColumns = computed<DisplayBoardColumn[]>(() =>
  activeColumns.value.map((column) => {
    if (!column.isCompletedState) {
      return column
    }

    return {
      ...column,
      tasks: column.tasks.filter((task) => isCompletedTaskVisible(task, completedTasksVisibility.value)),
    }
  }),
)

const completedColumnSummaries = computed<CompletedColumnSummary[]>(() =>
  activeColumns.value
    .filter((column) => column.isCompletedState)
    .map((column) => {
      const visibleColumn = displayColumns.value.find((candidate) => candidate.stateKey === column.stateKey)
      return {
        stateKey: column.stateKey,
        selectedLabel: getCompletedTasksVisibilityLabel(completedTasksVisibility.value),
        hiddenTaskCount: Math.max(0, column.tasks.length - (visibleColumn?.tasks.length ?? 0)),
      }
    }),
)

onMounted(async () => {
  removeSessionInvalidatedListener = onRonFlowSessionInvalidated(() => {
    handleSessionInvalidated()
  })

  const authenticated = await initialize()
  if (authenticated) {
    await initializeWorkspace()
  }
})

onUnmounted(() => {
  stopWorkspacePolling()
  void stopDatabaseSyncNotifications()
  removeSessionInvalidatedListener?.()
})

watch(isAuthenticated, (authenticated) => {
  if (!authenticated) {
    stopWorkspacePolling()
    void stopDatabaseSyncNotifications()
    isDatabaseSyncNotificationsOpen.value = false
    invitationInboxCount.value = 0
  }
})

watch(completedTasksVisibility, (value) => {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.setItem(COMPLETED_TASKS_VISIBILITY_STORAGE_KEY, value)
})

async function initializeWorkspace() {
  currentWorkspaceView.value = 'board'
  await activateRonFlowSession()
  void startDatabaseSyncNotifications()
  await Promise.all([
    loadProjects(),
    refreshInvitationInboxCount(),
  ])
  startWorkspacePolling()
}

async function refreshInvitationInboxCount() {
  try {
    const inbox = await projectQueryService.getInvitationInbox()
    invitationInboxCount.value = inbox.items.length
  } catch {}
}

async function pollWorkspace() {
  if (!isAuthenticated.value || isPollingWorkspace) {
    return
  }

  isPollingWorkspace = true

  try {
    await refreshProjectsSilently()

    if (currentWorkspaceView.value !== 'invitations') {
      await refreshBoardSilently()
      await refreshSelectedTaskDetailSilently()
    }

    if (currentWorkspaceView.value === 'reports') {
      await loadWorkflowThroughputReport(workflowThroughputBucket.value, true)
      await loadTaskAgingReport(taskAgingThresholds.value, true)
      await loadCycleTimeReport(cycleTimeRange.value, true)
      await loadCompletedTasksByMonthReport(completedTasksByMonthWindow.value, true)
    }

    await refreshInvitationInboxCount()
  } finally {
    isPollingWorkspace = false
  }
}

function startWorkspacePolling() {
  stopWorkspacePolling()
  workspacePollTimer = window.setInterval(() => {
    void pollWorkspace()
  }, 3000)
}

function stopWorkspacePolling() {
  if (workspacePollTimer !== null) {
    window.clearInterval(workspacePollTimer)
    workspacePollTimer = null
  }
}

function handleSessionInvalidated() {
  stopWorkspacePolling()
  void stopDatabaseSyncNotifications()
  clearLocalSession('RonFlow session 已失效，請重新登入。')
  currentWorkspaceView.value = 'board'
  isDatabaseSyncNotificationsOpen.value = false
  invitationInboxCount.value = 0
}

async function onLogin(payload: PasswordLoginInput) {
  const succeeded = await login(payload)
  if (succeeded) {
    await initializeWorkspace()
  }
}

async function onRegister(payload: RegisterUserInput) {
  const succeeded = await register(payload)
  if (succeeded) {
    await initializeWorkspace()
  }
}

async function onRefreshCurrentUser() {
  await loadCurrentUser()
}

function onChangeCompletedTasksVisibility(value: CompletedTasksVisibilityValue) {
  completedTasksVisibility.value = value
}

async function onLogout() {
  stopWorkspacePolling()
  await stopDatabaseSyncNotifications()
  await leaveActiveProjectScope()
  await logout()
  currentWorkspaceView.value = 'board'
  isDatabaseSyncNotificationsOpen.value = false
  invitationInboxCount.value = 0
}

function toggleDatabaseSyncNotifications() {
  isDatabaseSyncNotificationsOpen.value = !isDatabaseSyncNotificationsOpen.value

  if (isDatabaseSyncNotificationsOpen.value) {
    markDatabaseSyncNotificationsSeen()
  }
}

function formatDatabaseSyncStatus(status: DatabaseSyncOperationStatus) {
  switch (status) {
    case 'succeeded':
      return '完成'
    case 'failed':
      return '失敗'
    case 'running':
      return '同步中'
    default:
      return '排隊中'
  }
}

function formatDatabaseSyncTime(operation: DatabaseSyncOperationResponse) {
  return formatTimelineTime(operation.completedAt ?? operation.startedAt ?? operation.requestedAt)
}

function showDatabaseSyncToast(operation: DatabaseSyncOperationResponse) {
  const succeeded = operation.status === 'succeeded'
  toast.add({
    group: 'database-sync',
    severity: succeeded ? 'success' : 'error',
    summary: succeeded ? 'Git 同步完成' : 'Git 同步失敗',
    detail: operation.failureSummary ?? operation.reason,
    life: 5000,
  })
}

function showTaskNotificationToast(notification: TaskNotificationResponse) {
  toast.add({
    severity: 'success',
    summary: notification.summary,
    detail: notification.detail,
    life: 4000,
  })
}

async function leaveActiveProjectScope() {
  if (!activeProjectId.value) {
    return
  }

  closeTaskDetail()

  try {
    await releaseRonFlowProjectScope()
  } catch {}
}

function onOpenCreateTask() {
  if (activeProjectId.value) {
    createTaskModalRef.value?.open(activeProjectId.value)
  }
}

function openBoardView() {
  currentWorkspaceView.value = 'board'
}

async function openArchivedTasksView() {
  if (!activeProjectId.value) {
    return
  }

  currentWorkspaceView.value = 'archived'
  await loadArchivedTasks(activeProjectId.value)
}

async function openTrashView() {
  if (!activeProjectId.value) {
    return
  }

  currentWorkspaceView.value = 'trash'
  await loadTrashedTasks(activeProjectId.value)
}

async function openCodeTraceabilityView() {
  if (!activeProjectId.value) {
    return
  }

  currentWorkspaceView.value = 'codeTraceability'
  isLoadingCodeTraceability.value = true
  codeTraceabilityError.value = ''

  try {
    const response = await projectQueryService.getCodeTraceability(activeProjectId.value)
    codeTraceabilityItems.value = response.items
  } catch {
    codeTraceabilityError.value = '無法載入程式修改紀錄，請稍後再試。'
  } finally {
    isLoadingCodeTraceability.value = false
  }
}

async function openReportsView() {
  if (!activeProjectId.value) {
    return
  }

  currentWorkspaceView.value = 'reports'
  await Promise.all([
    loadWorkflowThroughputReport(workflowThroughputBucket.value),
    loadTaskAgingReport(taskAgingThresholds.value),
    loadCycleTimeReport(cycleTimeRange.value),
    loadCompletedTasksByMonthReport(completedTasksByMonthWindow.value),
  ])
}

async function loadWorkflowThroughputReport(bucket: 'day' | 'week', silent = false) {
  if (!activeProjectId.value) {
    return
  }

  workflowThroughputBucket.value = bucket

  if (!silent) {
    isLoadingWorkflowThroughput.value = true
  }

  workflowThroughputError.value = ''

  try {
    workflowThroughputReport.value = await projectQueryService.getWorkflowThroughput(activeProjectId.value, bucket)
  } catch {
    workflowThroughputError.value = '無法載入工作流量報表，請稍後再試。'
  } finally {
    if (!silent) {
      isLoadingWorkflowThroughput.value = false
    }
  }
}

async function loadTaskAgingReport(
  thresholds: { todoThresholdDays: number; activeThresholdDays: number; reviewThresholdDays: number },
  silent = false,
) {
  if (!activeProjectId.value) {
    return
  }

  taskAgingThresholds.value = { ...thresholds }

  if (!silent) {
    isLoadingTaskAging.value = true
  }

  taskAgingError.value = ''

  try {
    taskAgingReport.value = await projectQueryService.getTaskAging(activeProjectId.value, taskAgingThresholds.value)
  } catch {
    taskAgingError.value = '無法載入任務停留報表，請稍後再試。'
  } finally {
    if (!silent) {
      isLoadingTaskAging.value = false
    }
  }
}

async function loadCycleTimeReport(
  range: { completedFrom: string; completedTo: string },
  silent = false,
) {
  if (!activeProjectId.value) {
    return
  }

  cycleTimeRange.value = { ...range }

  if (!silent) {
    isLoadingCycleTime.value = true
  }

  cycleTimeError.value = ''

  try {
    cycleTimeReport.value = await projectQueryService.getCycleTime(activeProjectId.value, cycleTimeRange.value)
  } catch {
    cycleTimeError.value = '無法載入週期時間報表，請稍後再試。'
  } finally {
    if (!silent) {
      isLoadingCycleTime.value = false
    }
  }
}

async function loadCompletedTasksByMonthReport(
  options: { anchorMonth: string; monthCount: number },
  silent = false,
) {
  if (!activeProjectId.value) {
    return
  }

  completedTasksByMonthWindow.value = { ...options }

  if (!silent) {
    isLoadingCompletedTasksByMonth.value = true
  }

  completedTasksByMonthError.value = ''

  try {
    completedTasksByMonthReport.value = await projectQueryService.getCompletedTasksByMonth(activeProjectId.value, completedTasksByMonthWindow.value)
  } catch {
    completedTasksByMonthError.value = '無法載入已完成月份報表，請稍後再試。'
  } finally {
    if (!silent) {
      isLoadingCompletedTasksByMonth.value = false
    }
  }
}

function shiftCompletedMonthWindow(direction: 'older' | 'newer') {
  const monthDelta = direction === 'older' ? -1 : 1
  void loadCompletedTasksByMonthReport({
    ...completedTasksByMonthWindow.value,
    anchorMonth: addMonthsToIsoDate(completedTasksByMonthWindow.value.anchorMonth, monthDelta),
  })
}

async function onSelectProject(projectId: string) {
  if (activeProjectId.value && activeProjectId.value !== projectId) {
    stopWorkspacePolling()
    await leaveActiveProjectScope()
  }

  currentWorkspaceView.value = 'board'
  await selectProject(projectId)
  startWorkspacePolling()
}

async function onOpenTaskDetail(taskId: string, taskTitle: string) {
  await openTaskDetail(taskId, 'active', taskTitle)
}

async function onOpenLifecycleTaskDetail(taskId: string, mode: Exclude<TaskDetailMode, 'active'>, taskTitle: string) {
  await openTaskDetail(taskId, mode, taskTitle)
}

async function onOpenCodeTraceabilityTaskDetail(taskId: string, taskTitle: string) {
  await openTaskDetail(taskId, 'active', taskTitle, { forceReadOnly: true })
}

async function onProjectCreated(projectId: string) {
  currentWorkspaceView.value = 'board'
  await loadProjects(projectId)
}

async function onTaskCreated() {
  if (activeProjectId.value) {
    await Promise.all([loadProjects(activeProjectId.value), loadBoard(activeProjectId.value)])
  }
}

async function onTaskDetailSave(payload: {
  taskId: string
  title: string
  description: string
  dueDate: string | null
  estimatedEffort: TaskEstimatedEffortResponse | null
  isShort: boolean
  codeTraceability: EditableTaskCodeTraceability
  subtasks: EditableTaskSubtask[]
}) {
  await updateTaskDetail(
    payload.taskId,
    payload.title,
    payload.description,
    payload.dueDate,
    payload.estimatedEffort,
    payload.isShort,
    payload.codeTraceability,
    payload.subtasks,
  )
}

async function onSendTaskToFlow(taskId: string) {
  await sendTaskToFlow(taskId)
}

async function onMoveTaskToState(taskId: string, stateKey: WorkflowKey) {
  await moveTaskToState(taskId, stateKey)
}

async function onReplaceTaskSubtasks(payload: { taskId: string; subtasks: EditableTaskSubtask[] }) {
  await replaceTaskSubtasks(payload.taskId, payload.subtasks)
}

async function onSetTaskSplitComplete(payload: { taskId: string; isSplitComplete: boolean }) {
  await setTaskSplitComplete(payload.taskId, payload.isSplitComplete)
}

async function onCreateChildTask(payload: { parentTaskId: string; title: string }) {
  await createChildTask(payload.parentTaskId, payload.title)
}

async function onAddReminder(payload: { taskId: string; reminderDateTime: string; description: string }) {
  await createReminder(payload.taskId, payload.reminderDateTime, payload.description)
}

async function onDeleteReminder(payload: { taskId: string; reminderId: string }) {
  await deleteReminder(payload.taskId, payload.reminderId)
}

async function onArchiveTask(taskId: string) {
  await archiveTask(taskId)
  currentWorkspaceView.value = 'board'
}

async function onMoveTaskToTrash(taskId: string) {
  await moveTaskIntoTrash(taskId)
  currentWorkspaceView.value = 'board'
}

async function onDuplicateTaskSubtree(taskId: string) {
  await duplicateTaskSubtree(taskId)
  currentWorkspaceView.value = 'board'
}

async function onRestoreTask(taskId: string, mode: Exclude<TaskDetailMode, 'active'>) {
  const restored = mode === 'archived'
    ? await restoreArchivedTask(taskId)
    : await restoreTrashedTask(taskId)

  if (restored) {
    currentWorkspaceView.value = 'board'
  }
}

function openProjectMembersPanel() {
  if (!activeProjectId.value || activeProject.value?.role === '專案成員') {
    return
  }

  currentWorkspaceView.value = 'members'
}

async function loadProjectSubtaskTemplates(projectId: string) {
  isLoadingProjectSubtaskTemplates.value = true
  projectSubtaskTemplatesError.value = ''

  try {
    const response = await projectQueryService.getSubtaskTemplates(projectId)
    projectSubtaskTemplates.value = response.items
  } catch {
    projectSubtaskTemplatesError.value = '無法載入完成條件模板，請稍後再試。'
  } finally {
    isLoadingProjectSubtaskTemplates.value = false
  }
}

async function openProjectSubtaskTemplatesModal() {
  if (!activeProjectId.value) {
    return
  }

  isProjectSubtaskTemplatesOpen.value = true
  projectSubtaskTemplatesCommandError.value = ''
  await loadProjectSubtaskTemplates(activeProjectId.value)
}

function closeProjectSubtaskTemplatesModal() {
  if (isSavingProjectSubtaskTemplates.value) {
    return
  }

  isProjectSubtaskTemplatesOpen.value = false
  projectSubtaskTemplatesError.value = ''
  projectSubtaskTemplatesCommandError.value = ''
}

async function onSaveProjectSubtaskTemplates(payload: Array<{ id: string | null; title: string; order: number }>) {
  if (!activeProjectId.value) {
    return
  }

  isSavingProjectSubtaskTemplates.value = true
  projectSubtaskTemplatesCommandError.value = ''

  try {
    const response = await projectCommandService.replaceSubtaskTemplates(activeProjectId.value, payload)
    projectSubtaskTemplates.value = response.items
    isProjectSubtaskTemplatesOpen.value = false
  } catch (error) {
    if (error instanceof ApiValidationError) {
      projectSubtaskTemplatesCommandError.value = error.errors.items?.[0] ?? '完成條件標題為必填欄位'
    } else {
      projectSubtaskTemplatesCommandError.value = '儲存完成條件模板失敗，請稍後再試。'
    }
  } finally {
    isSavingProjectSubtaskTemplates.value = false
  }
}

async function openInvitationInbox() {
  stopWorkspacePolling()
  await leaveActiveProjectScope()
  currentWorkspaceView.value = 'invitations'
  startWorkspacePolling()
}

async function onInvitationAccepted() {
  await loadProjects(activeProjectId.value ?? undefined)
  await refreshInvitationInboxCount()
}

async function onInvitationsChanged() {
  await loadProjects(activeProjectId.value ?? undefined)
  await refreshInvitationInboxCount()
}
</script>
