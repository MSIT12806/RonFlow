

<template>
  <main class="app-shell">
    <div class="ambient ambient-left"></div>
    <div class="ambient ambient-right"></div>

    <section class="workspace-shell">
      <header class="topbar">
        <div>
          <p class="eyebrow">RonFlow</p>
          <h1 class="app-title">專案流程看板</h1>
          <p class="app-subtitle">以真後端 API 驅動專案看板，讓使用者流程與後端規則保持一致。</p>
        </div>

        <button type="button" class="primary-button" @click="openCreateProjectModal">
          建立專案
        </button>
      </header>

      <p v-if="pageError" class="error-copy">{{ pageError }}</p>

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
          @open-create-task="openCreateTaskModal"
          @open-task-detail="openTaskDetail"
          @move-task-to-state="moveTaskToState"
        />
      </section>
    </section>

    <CreateProjectModal
      :is-open="isCreateProjectOpen"
      :project-name="projectNameInput"
      :project-name-error="projectNameError"
      :is-submitting="isSubmittingProject"
      @close="closeCreateProjectModal"
      @submit="submitProject"
      @update:project-name="projectNameInput = $event"
    />

    <CreateTaskModal
      :is-open="isCreateTaskOpen"
      :task-title="taskTitleInput"
      :task-title-error="taskTitleError"
      :is-submitting="isSubmittingTask"
      @close="closeCreateTaskModal"
      @submit="submitTask"
      @update:task-title="taskTitleInput = $event"
    />

    <TaskDetailModal
      :is-open="isTaskDetailOpen"
      :task="selectedTask"
      :format-timeline-time="formatTimelineTime"
      @close="closeTaskDetail"
    />
  </main>
</template>

<script setup lang="ts">
import CreateProjectModal from './components/CreateProjectModal.vue'
import CreateTaskModal from './components/CreateTaskModal.vue'
import ProjectBoard from './components/ProjectBoard.vue'
import ProjectSidebar from './components/ProjectSidebar.vue'
import TaskDetailModal from './components/TaskDetailModal.vue'
import { useRonFlowBoard } from './composables/useRonFlowBoard'

const {
  projects,
  activeProjectId,
  activeProject,
  activeColumns,
  selectedTask,
  isCreateProjectOpen,
  isCreateTaskOpen,
  isTaskDetailOpen,
  isLoadingProjects,
  isLoadingBoard,
  isSubmittingProject,
  isSubmittingTask,
  projectNameInput,
  taskTitleInput,
  projectNameError,
  taskTitleError,
  pageError,
  openCreateProjectModal,
  closeCreateProjectModal,
  submitProject,
  openCreateTaskModal,
  closeCreateTaskModal,
  submitTask,
  openTaskDetail,
  selectProject,
  closeTaskDetail,
  moveTaskToState,
  formatProjectMeta,
  formatTimelineTime,
} = useRonFlowBoard()
</script>