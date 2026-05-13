

<template>
  <AsyncStatePlayground v-if="showAsyncStatePlayground" />

  <main v-else class="app-shell">
    <div class="ambient ambient-left"></div>
    <div class="ambient ambient-right"></div>

    <section class="workspace-shell">
      <header class="topbar">
        <div>
          <p class="eyebrow">RonFlow</p>
          <h1 class="app-title">專案流程看板</h1>
          <p class="app-subtitle">以真後端 API 驅動專案看板，讓使用者流程與後端規則保持一致。</p>
        </div>

        <button type="button" class="primary-button" @click="createProjectModalRef?.open()">
          建立專案
        </button>
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
            :is-loading-projects="isLoadingProjects"
            :has-error="Boolean(pageError)"
            :format-project-meta="formatProjectMeta"
            @select-project="onSelectProject"
          />

          <ProjectBoard
            v-if="currentWorkspaceView === 'board'"
            :active-project-name="activeProject?.name ?? null"
            :columns="activeColumns"
            :is-loading-board="isLoadingBoard"
            :command-error-message="boardCommandError"
            @open-create-task="onOpenCreateTask"
            @open-archived-tasks="openArchivedTasksView"
            @open-trash-view="openTrashView"
            @open-task-detail="openTaskDetail"
            @move-task-to-state="moveTaskToState"
            @reorder-task-within-column="reorderTaskWithinColumn"
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
            @open-task-detail="onOpenLifecycleTaskDetail($event, 'archived')"
            @restore-task="onRestoreTask($event, 'archived')"
          />

          <LifecycleTaskListView
            v-else
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
            @open-task-detail="onOpenLifecycleTaskDetail($event, 'trashed')"
            @restore-task="onRestoreTask($event, 'trashed')"
          />
        </section>
      </AsyncStateBoundary>
    </section>

    <CreateProjectModal
      ref="createProjectModalRef"
      @project-created="onProjectCreated"
    />

    <CreateTaskModal
      ref="createTaskModalRef"
      @task-created="onTaskCreated"
    />

    <TaskDetailModal
      :is-open="isTaskDetailOpen"
      :is-loading="isLoadingTaskDetail"
      :is-saving="isUpdatingTaskDetail"
      :error-message="taskDetailError"
      :save-error-message="taskDetailCommandError"
      :title-validation-error="taskTitleValidationError"
      :mode="taskDetailMode"
      :task="selectedTask"
      :format-timeline-time="formatTimelineTime"
      @close="closeTaskDetail"
      @save="onTaskDetailSave"
      @archive="onArchiveTask"
      @move-to-trash="onMoveTaskToTrash"
      @restore="onRestoreTask"
    />
  </main>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import AsyncStatePlayground from './devtools/playground/AsyncStatePlayground.vue'
import AsyncStateBoundary from './components/bases/AsyncStateBoundary.vue'
import CreateProjectModal from './components/CreateProjectModal.vue'
import CreateTaskModal from './components/CreateTaskModal.vue'
import LifecycleTaskListView from './components/LifecycleTaskListView.vue'
import ProjectBoard from './components/ProjectBoard.vue'
import ProjectSidebar from './components/ProjectSidebar.vue'
import TaskDetailModal from './components/TaskDetailModal.vue'
import { useRonFlowBoard, type TaskDetailMode } from './composables/useRonFlowBoard'

type WorkspaceView = 'board' | 'archived' | 'trash'

const showAsyncStatePlayground = import.meta.env.DEV
  && new URLSearchParams(window.location.search).get('playground') === 'async-states'

const createProjectModalRef = ref<InstanceType<typeof CreateProjectModal> | null>(null)
const createTaskModalRef = ref<InstanceType<typeof CreateTaskModal> | null>(null)
const currentWorkspaceView = ref<WorkspaceView>('board')

const {
  projects,
  activeProjectId,
  activeProject,
  activeColumns,
  selectedTask,
  taskDetailMode,
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
  openTaskDetail,
  selectProject,
  closeTaskDetail,
  moveTaskToState,
  updateTaskDetail,
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
  loadBoard,
} = useRonFlowBoard()

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
  currentWorkspaceView.value = 'board'
  await selectProject(projectId)
}

async function onOpenLifecycleTaskDetail(taskId: string, mode: Exclude<TaskDetailMode, 'active'>) {
  await openTaskDetail(taskId, mode)
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
</script>