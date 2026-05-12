

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
            @select-project="selectProject"
          />

          <ProjectBoard
            :active-project-name="activeProject?.name ?? null"
            :columns="activeColumns"
            :is-loading-board="isLoadingBoard"
            :command-error-message="boardCommandError"
            @open-create-task="onOpenCreateTask"
            @open-task-detail="openTaskDetail"
            @move-task-to-state="moveTaskToState"
            @reorder-task-within-column="reorderTaskWithinColumn"
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
      :task="selectedTask"
      :format-timeline-time="formatTimelineTime"
      @close="closeTaskDetail"
      @save="onTaskDetailSave"
    />
  </main>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import AsyncStatePlayground from './devtools/playground/AsyncStatePlayground.vue'
import AsyncStateBoundary from './components/bases/AsyncStateBoundary.vue'
import CreateProjectModal from './components/CreateProjectModal.vue'
import CreateTaskModal from './components/CreateTaskModal.vue'
import ProjectBoard from './components/ProjectBoard.vue'
import ProjectSidebar from './components/ProjectSidebar.vue'
import TaskDetailModal from './components/TaskDetailModal.vue'
import { useRonFlowBoard } from './composables/useRonFlowBoard'

const showAsyncStatePlayground = import.meta.env.DEV
  && new URLSearchParams(window.location.search).get('playground') === 'async-states'

const createProjectModalRef = ref<InstanceType<typeof CreateProjectModal> | null>(null)
const createTaskModalRef = ref<InstanceType<typeof CreateTaskModal> | null>(null)

const {
  projects,
  activeProjectId,
  activeProject,
  activeColumns,
  selectedTask,
  isTaskDetailOpen,
  isLoadingProjects,
  isLoadingBoard,
  isLoadingTaskDetail,
  isUpdatingTaskDetail,
  taskDetailError,
  taskDetailCommandError,
  pageError,
  boardCommandError,
  taskTitleValidationError,
  openTaskDetail,
  selectProject,
  closeTaskDetail,
  moveTaskToState,
  updateTaskDetail,
  reorderTaskWithinColumn,
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

async function onProjectCreated(projectId: string) {
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
</script>