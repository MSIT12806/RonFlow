<script setup lang="ts">
import { computed, ref } from 'vue'

type WorkflowKey = 'todo' | 'active' | 'review' | 'done'

type TaskItem = {
  id: number
  title: string
  status: WorkflowKey
  history: string[]
}

type ProjectItem = {
  id: number
  name: string
  tasks: TaskItem[]
}

const workflowColumns: Array<{ key: WorkflowKey; label: string }> = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
]

const projects = ref<ProjectItem[]>([])
const activeProjectId = ref<number | null>(null)
const selectedTaskId = ref<number | null>(null)

const isCreateProjectOpen = ref(false)
const isCreateTaskOpen = ref(false)
const isTaskDetailOpen = ref(false)

const projectNameInput = ref('')
const taskTitleInput = ref('')
const projectNameError = ref('')
const taskTitleError = ref('')

let nextProjectId = 1
let nextTaskId = 1

const activeProject = computed(() =>
  projects.value.find((project) => project.id === activeProjectId.value) ?? null,
)

const selectedTask = computed(() =>
  activeProject.value?.tasks.find((task) => task.id === selectedTaskId.value) ?? null,
)

function openCreateProjectModal() {
  projectNameInput.value = ''
  projectNameError.value = ''
  isCreateProjectOpen.value = true
}

function closeCreateProjectModal() {
  isCreateProjectOpen.value = false
}

function submitProject() {
  const name = projectNameInput.value.trim()

  if (!name) {
    projectNameError.value = '專案名稱為必填欄位'
    return
  }

  const project: ProjectItem = {
    id: nextProjectId,
    name,
    tasks: [],
  }

  nextProjectId += 1
  projects.value = [...projects.value, project]
  activeProjectId.value = project.id
  closeCreateProjectModal()
}

function openCreateTaskModal() {
  if (!activeProject.value) {
    return
  }

  taskTitleInput.value = ''
  taskTitleError.value = ''
  isCreateTaskOpen.value = true
}

function closeCreateTaskModal() {
  isCreateTaskOpen.value = false
}

function submitTask() {
  const title = taskTitleInput.value.trim()

  if (!title) {
    taskTitleError.value = '任務標題為必填欄位'
    return
  }

  if (!activeProject.value) {
    return
  }

  const createdTask: TaskItem = {
    id: nextTaskId,
    title,
    status: 'todo',
    history: ['已建立任務'],
  }

  nextTaskId += 1

  projects.value = projects.value.map((project) =>
    project.id === activeProject.value?.id
      ? {
          ...project,
          tasks: [...project.tasks, createdTask],
        }
      : project,
  )

  selectedTaskId.value = createdTask.id
  closeCreateTaskModal()
}

function openTaskDetail(taskId: number) {
  selectedTaskId.value = taskId
  isTaskDetailOpen.value = true
}

function closeTaskDetail() {
  isTaskDetailOpen.value = false
}

function getTasksByStatus(status: WorkflowKey) {
  return activeProject.value?.tasks.filter((task) => task.status === status) ?? []
}

function getWorkflowLabel(status: WorkflowKey) {
  return workflowColumns.find((column) => column.key === status)?.label ?? status
}
</script>

<template>
  <main class="app-shell">
    <div class="ambient ambient-left"></div>
    <div class="ambient ambient-right"></div>

    <section class="workspace-shell">
      <header class="topbar">
        <div>
          <p class="eyebrow">RonFlow</p>
          <h1 class="app-title">專案流程看板</h1>
          <p class="app-subtitle">先完成可驗證的前端流程，再逐步接上 API 與持久化。</p>
        </div>

        <button type="button" class="primary-button" @click="openCreateProjectModal">
          建立專案
        </button>
      </header>

      <section class="workspace-layout">
        <aside class="project-panel">
          <div class="panel-heading-row">
            <div>
              <p class="eyebrow">Workspace</p>
              <h2 class="panel-title">專案列表</h2>
            </div>
            <span class="count-badge">{{ projects.length }}</span>
          </div>

          <p v-if="projects.length === 0" class="empty-copy">尚未建立任何專案</p>

          <ul v-else class="project-list">
            <li v-for="project in projects" :key="project.id">
              <button
                type="button"
                class="project-chip"
                :class="{ 'project-chip-active': project.id === activeProjectId }"
                @click="activeProjectId = project.id"
              >
                <span>{{ project.name }}</span>
                <small>{{ project.tasks.length }} tasks</small>
              </button>
            </li>
          </ul>
        </aside>

        <section class="board-panel">
          <template v-if="activeProject">
            <header class="board-header">
              <div>
                <p class="eyebrow">Current board</p>
                <h2 class="board-title">{{ activeProject.name }}</h2>
              </div>

              <button type="button" class="primary-button" @click="openCreateTaskModal">
                建立任務
              </button>
            </header>

            <div class="board-grid">
              <article
                v-for="column in workflowColumns"
                :key="column.key"
                :data-testid="`workflow-column-${column.key}`"
                class="board-column"
              >
                <header class="column-header">
                  <h3>{{ column.label }}</h3>
                  <span class="count-badge">{{ getTasksByStatus(column.key).length }}</span>
                </header>

                <div v-if="getTasksByStatus(column.key).length === 0" class="column-empty">
                  目前沒有任務
                </div>

                <div v-else class="task-list">
                  <button
                    v-for="task in getTasksByStatus(column.key)"
                    :key="task.id"
                    type="button"
                    class="task-card"
                    @click="openTaskDetail(task.id)"
                  >
                    <span class="task-title">{{ task.title }}</span>
                    <span class="task-meta">{{ getWorkflowLabel(task.status) }}</span>
                  </button>
                </div>
              </article>
            </div>
          </template>

          <div v-else class="board-empty-state">
            <p class="eyebrow">Outside-In slice</p>
            <h2>先建立第一個專案，再展開任務看板。</h2>
            <p>
              目前首頁已經具備專案建立入口，建立後會立即顯示預設 workflow 欄位與任務入口。
            </p>
          </div>
        </section>
      </section>
    </section>

    <div v-if="isCreateProjectOpen" class="modal-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="create-project-title" class="modal-card">
        <div class="modal-header">
          <div>
            <p class="eyebrow">New project</p>
            <h2 id="create-project-title">建立專案</h2>
          </div>
          <button type="button" class="ghost-icon-button" aria-label="關閉視窗" @click="closeCreateProjectModal">
            ×
          </button>
        </div>

        <form class="modal-form" @submit.prevent="submitProject">
          <label for="project-name">專案名稱</label>
          <input id="project-name" v-model="projectNameInput" type="text" autocomplete="off" />
          <p v-if="projectNameError" class="error-copy">{{ projectNameError }}</p>

          <div class="modal-actions">
            <button type="button" class="secondary-button" @click="closeCreateProjectModal">取消</button>
            <button type="submit" class="primary-button">建立</button>
          </div>
        </form>
      </div>
    </div>

    <div v-if="isCreateTaskOpen" class="modal-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="create-task-title" class="modal-card">
        <div class="modal-header">
          <div>
            <p class="eyebrow">New task</p>
            <h2 id="create-task-title">建立任務</h2>
          </div>
          <button type="button" class="ghost-icon-button" aria-label="關閉視窗" @click="closeCreateTaskModal">
            ×
          </button>
        </div>

        <form class="modal-form" @submit.prevent="submitTask">
          <label for="task-title">任務標題</label>
          <input id="task-title" v-model="taskTitleInput" type="text" autocomplete="off" />
          <p v-if="taskTitleError" class="error-copy">{{ taskTitleError }}</p>

          <div class="modal-actions">
            <button type="button" class="secondary-button" @click="closeCreateTaskModal">取消</button>
            <button type="submit" class="primary-button">建立</button>
          </div>
        </form>
      </div>
    </div>

    <div v-if="isTaskDetailOpen && selectedTask" class="modal-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="task-detail-title" class="modal-card modal-card-wide">
        <div class="modal-header">
          <div>
            <p class="eyebrow">Task detail</p>
            <h2 id="task-detail-title">任務詳細資訊</h2>
          </div>
          <button type="button" class="ghost-icon-button" aria-label="關閉視窗" @click="closeTaskDetail">
            ×
          </button>
        </div>

        <section class="detail-layout">
          <div class="detail-card">
            <p class="detail-label">任務標題</p>
            <h3>{{ selectedTask.title }}</h3>
          </div>

          <div class="detail-card">
            <p class="detail-label">狀態</p>
            <strong>{{ getWorkflowLabel(selectedTask.status) }}</strong>
          </div>

          <div class="detail-card detail-card-full">
            <p class="detail-label">活動紀錄</p>
            <ul class="history-list">
              <li v-for="entry in selectedTask.history" :key="entry">{{ entry }}</li>
            </ul>
          </div>
        </section>
      </div>
    </div>
  </main>
</template>