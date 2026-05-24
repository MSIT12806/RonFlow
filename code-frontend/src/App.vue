

<template>
  <AsyncStatePlayground v-if="showAsyncStatePlayground" />

  <main v-else class="app-shell">
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
        <section class="workspace-layout">
          <ProjectSidebar
            :projects="projects"
            :active-project-id="activeProjectId"
            :invitation-inbox-count="invitationInboxCount"
            :is-loading-projects="isLoadingProjects"
            :has-error="Boolean(pageError)"
            :format-project-meta="formatProjectMeta"
            @select-project="onSelectProject"
            @open-invitation-inbox="openInvitationInbox"
          />

          <ProjectBoard
            v-if="currentWorkspaceView === 'board'"
            :active-project-name="activeProject?.name ?? null"
            :columns="activeColumns"
            :is-loading-board="isLoadingBoard"
            :command-error-message="boardCommandError"
            :can-manage-members="activeProject?.role !== '專案成員'"
            @open-create-task="onOpenCreateTask"
            @open-project-members="openProjectMembersPanel"
            @open-archived-tasks="openArchivedTasksView"
            @open-trash-view="openTrashView"
            @open-task-detail="onOpenTaskDetail"
            @move-task-to-state="moveTaskToState"
            @reorder-task-within-column="reorderTaskWithinColumn"
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
      :can-enter-edit="selectedTask?.canEnterEdit ?? true"
      :error-message="taskDetailError"
      :save-error-message="taskDetailCommandError"
      :title-validation-error="taskTitleValidationError"
      :reminder-datetime-validation-error="reminderDateTimeValidationError"
      :reminder-delivery-status-message="reminderDeliveryStatusMessage"
      :can-enable-reminder-delivery="canEnableReminderDelivery"
      :is-enabling-reminder-delivery="isEnablingReminderDelivery"
      :mode="taskDetailMode"
      :display-title="taskDetailDisplayTitle"
      :task="selectedTask"
      :format-timeline-time="formatTimelineTime"
      @close="closeTaskDetail"
      @enter-edit="enterTaskDetailEditMode"
      @save="onTaskDetailSave"
      @add-reminder="onAddReminder"
      @delete-reminder="onDeleteReminder"
      @enable-reminder-delivery="enableReminderDelivery"
      @archive="onArchiveTask"
      @move-to-trash="onMoveTaskToTrash"
      @restore="onRestoreTask"
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
import { onMounted, onUnmounted, ref, watch } from 'vue'
import AsyncStatePlayground from './devtools/playground/AsyncStatePlayground.vue'
import AsyncStateBoundary from './components/bases/AsyncStateBoundary.vue'
import CreateProjectModal from './components/CreateProjectModal.vue'
import CreateTaskModal from './components/CreateTaskModal.vue'
import InvitationInboxView from './components/InvitationInboxView.vue'
import LifecycleTaskListView from './components/LifecycleTaskListView.vue'
import ProjectBoard from './components/ProjectBoard.vue'
import ProjectMembersPanel from './components/ProjectMembersPanel.vue'
import ProjectSidebar from './components/ProjectSidebar.vue'
import RonAuthEntryPanel from './components/RonAuthEntryPanel.vue'
import TaskDetailModal from './components/TaskDetailModal.vue'
import type { PasswordLoginInput, RegisterUserInput } from './api/ronauth'
import { activateRonFlowSession, releaseRonFlowProjectScope } from './api/ronflowApi'
import { ProjectQueryService } from './application'
import { usePushNotifications } from './composables/usePushNotifications'
import { useRonFlowAuth } from './composables/useRonFlowAuth'
import { useRonFlowBoard, type TaskDetailMode } from './composables/useRonFlowBoard'
import { onRonFlowSessionInvalidated } from './ronflowSession'

type WorkspaceView = 'board' | 'members' | 'invitations' | 'archived' | 'trash'

const showAsyncStatePlayground = import.meta.env.DEV
  && new URLSearchParams(window.location.search).get('playground') === 'async-states'

const createProjectModalRef = ref<InstanceType<typeof CreateProjectModal> | null>(null)
const createTaskModalRef = ref<InstanceType<typeof CreateTaskModal> | null>(null)
const currentWorkspaceView = ref<WorkspaceView>('board')
const invitationInboxCount = ref(0)

const projectQueryService = new ProjectQueryService()
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
  projects,
  activeProjectId,
  activeProject,
  activeColumns,
  selectedTask,
  taskDetailDisplayTitle,
  taskDetailMode,
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
  createReminder,
  deleteReminder,
  reorderTaskWithinColumn,
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
  removeSessionInvalidatedListener?.()
})

watch(isAuthenticated, (authenticated) => {
  if (!authenticated) {
    stopWorkspacePolling()
    invitationInboxCount.value = 0
  }
})

async function initializeWorkspace() {
  currentWorkspaceView.value = 'board'
  await activateRonFlowSession()
  await loadProjects()
  await refreshInvitationInboxCount()
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
  clearLocalSession('RonFlow session 已失效，請重新登入。')
  currentWorkspaceView.value = 'board'
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

async function onLogout() {
  stopWorkspacePolling()
  await leaveActiveProjectScope()
  await logout()
  currentWorkspaceView.value = 'board'
  invitationInboxCount.value = 0
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

async function onProjectCreated(projectId: string) {
  currentWorkspaceView.value = 'board'
  await loadProjects(projectId)
}

async function onTaskCreated() {
  if (activeProjectId.value) {
    await Promise.all([loadProjects(activeProjectId.value), loadBoard(activeProjectId.value)])
  }
}

async function onTaskDetailSave(payload: { taskId: string; title: string; description: string; dueDate: string | null }) {
  await updateTaskDetail(payload.taskId, payload.title, payload.description, payload.dueDate)
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