<template>
  <section class="board-panel">
    <template v-if="activeProjectName">
      <header class="board-header">
        <div>
          <p class="eyebrow">Current board</p>
          <h2 class="board-title">{{ activeProjectName }}</h2>
        </div>

        <div class="board-header-actions">
          <button
            v-if="canManageMembers"
            type="button"
            class="secondary-button"
            @click="$emit('open-project-subtask-templates')"
          >
            完成條件模板
          </button>

          <button
            v-if="canManageMembers"
            type="button"
            class="secondary-button"
            @click="$emit('open-project-members')"
          >
            專案成員
          </button>

          <button type="button" class="secondary-button" @click="$emit('open-archived-tasks')">
            已封存任務
          </button>

          <button type="button" class="secondary-button" @click="$emit('open-code-traceability')">
            程式修改紀錄
          </button>

          <button type="button" class="secondary-button" @click="$emit('open-reports')">
            報表
          </button>

          <button type="button" class="secondary-button" @click="$emit('open-trash-view')">
            垃圾桶
          </button>

          <button type="button" class="primary-button" @click="$emit('open-create-task')">
            建立任務
          </button>
        </div>
      </header>

      <AsyncStateBoundary
        :is-loading="isLoadingBoard"
        error-message=""
        loading-message="正在載入專案看板..."
      >
        <BaseErrorState
          v-if="commandErrorMessage"
          :message="commandErrorMessage"
        />

        <section class="task-tree-panel" aria-labelledby="task-tree-title">
          <header class="task-tree-header">
            <div>
              <p class="eyebrow">Hatchery</p>
              <h3 id="task-tree-title">任務樹</h3>
            </div>
            <span class="count-badge">{{ taskTreeNodeCount }}</span>
          </header>

          <div v-if="taskTree.length === 0" class="task-tree-empty">
            目前沒有任務樹任務
          </div>

          <ul v-else class="task-tree-list">
            <TaskTreeNode
              v-for="task in taskTree"
              :key="task.id"
              :task="task"
              :parent-task-id="null"
              :dragging-task-id="draggingTaskSource?.source === 'tree' ? draggingTaskSource.taskId : null"
              :active-drop-target="activeTaskTreeDropTarget"
              @open-task-detail="(taskId, taskTitle) => $emit('open-task-detail', taskId, taskTitle)"
              @task-drag-start="handleTaskTreeDragStart"
              @task-drag-end="handleTaskTreeDragEnd"
              @task-drag-over="handleTaskTreeDragOver"
              @task-drag-leave="handleTaskTreeDragLeave"
              @task-drop="handleTaskTreeDrop"
            />
          </ul>
        </section>

        <div class="board-grid">
          <article
            v-for="column in columns"
            :key="column.stateKey"
            :data-testid="`workflow-column-${column.stateKey}`"
            class="board-column"
            :class="{ 'board-column-drop-target': dragOverStateKey === column.stateKey }"
            @dragenter.prevent="handleColumnDragEnter(column.stateKey)"
            @dragover.prevent="handleColumnDragOver($event, column.stateKey)"
            @dragleave="handleColumnDragLeave(column.stateKey)"
            @drop.prevent="handleTaskDrop($event, column.stateKey)"
          >
            <header class="column-header">
              <div>
                <h3>{{ column.label }}</h3>
                <p
                  v-if="getCompletedColumnSummary(column.stateKey)"
                  class="column-filter-note"
                >
                  {{ getCompletedColumnSummary(column.stateKey) }}
                </p>
              </div>
              <span class="count-badge">{{ column.tasks.length }}</span>
            </header>

            <div v-if="column.tasks.length === 0" class="column-empty">
              {{ column.emptyStateMessage }}
            </div>

            <div v-else class="task-list">
              <article
                v-for="task in column.tasks"
                :key="task.id"
                class="task-card"
                :class="{ 'task-card-drop-target': dragOverTaskId === task.id }"
                @dragenter.prevent="handleTaskCardDragEnter(task.id)"
                @dragover.prevent="handleTaskCardDragOver($event, task.id)"
                @dragleave="handleTaskCardDragLeave(task.id)"
                @drop.prevent="handleTaskCardDrop($event, column.stateKey, task.id)"
              >
                <button
                  type="button"
                  class="task-card-main"
                  draggable="true"
                  @dragstart="handleTaskDragStart($event, task.id)"
                  @dragend="handleTaskDragEnd"
                  @click="$emit('open-task-detail', task.id, task.title)"
                >
                  <span class="task-title">{{ task.title }}</span>
                  <span class="task-meta">{{ column.label }}</span>
                  <span v-if="task.parentPath" class="task-parent-path">來自：{{ task.parentPath }}</span>
                </button>
              </article>
            </div>
          </article>
        </div>
      </AsyncStateBoundary>
    </template>

    <div v-else class="board-empty-state">
      <p class="eyebrow">Outside-In slice</p>
      <h2>先建立第一個專案，再展開任務看板。</h2>
      <p>
        現在會直接向後端讀取專案與任務資料，建立後也會同步刷新看板狀態。
      </p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import BaseErrorState from './bases/BaseErrorState.vue'
import TaskTreeNode from './TaskTreeNode.vue'
import type { BoardColumnResponse, BoardTaskCardResponse, WorkflowKey } from '../api/ronflowApi'

const props = defineProps<{
  activeProjectName: string | null
  taskTree: BoardTaskCardResponse[]
  columns: BoardColumnResponse[]
  isLoadingBoard: boolean
  commandErrorMessage: string
  canManageMembers?: boolean
  completedColumnSummaries?: Array<{
    stateKey: string
    selectedLabel: string
    hiddenTaskCount: number
  }>
}>()

const emit = defineEmits<{
  (event: 'open-create-task'): void
  (event: 'open-project-subtask-templates'): void
  (event: 'open-project-members'): void
  (event: 'open-archived-tasks'): void
  (event: 'open-code-traceability'): void
  (event: 'open-reports'): void
  (event: 'open-trash-view'): void
  (event: 'open-task-detail', taskId: string, taskTitle: string): void
  (event: 'move-task-to-state', taskId: string, stateKey: WorkflowKey): void
  (event: 'reorder-task-within-column', taskId: string, targetTaskId: string): void
  (event: 'move-task-within-tree', taskId: string, payload: {
    targetParentTaskId: string | null
    targetSiblingTaskId: string | null
    insertAfter: boolean
  }): void
}>()

const taskTreeNodeCount = computed(() => countTaskTreeNodes(props.taskTree))

function countTaskTreeNodes(tasks: BoardTaskCardResponse[]): number {
  return tasks.reduce((total, task) => total + 1 + countTaskTreeNodes(task.children), 0)
}

function getCompletedColumnSummary(stateKey: string) {
  const summary = props.completedColumnSummaries?.find((item) => item.stateKey === stateKey)
  if (!summary) {
    return ''
  }

  return summary.hiddenTaskCount > 0
    ? `篩選：${summary.selectedLabel}，已隱藏 ${summary.hiddenTaskCount} 筆較早完成任務`
    : `篩選：${summary.selectedLabel}`
}

const draggingTaskId = ref<string | null>(null)
const dragOverStateKey = ref<WorkflowKey | null>(null)
const dragOverTaskId = ref<string | null>(null)
const draggingTaskSource = ref<{ taskId: string; source: 'board' | 'tree' } | null>(null)
const activeTaskTreeDropTarget = ref<{ taskId: string; placement: 'before' | 'after' | 'inside' } | null>(null)

type TaskTreeDropPlacement = 'before' | 'after' | 'inside'

type TaskTreeDropEventPayload = {
  taskId: string
  parentTaskId: string | null
  placement: TaskTreeDropPlacement
}

function handleTaskDragStart(event: DragEvent, taskId: string) {
  draggingTaskId.value = taskId
  draggingTaskSource.value = { taskId, source: 'board' }

  if (!event.dataTransfer) {
    return
  }

  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData('text/plain', taskId)
}

function handleTaskDragEnd() {
  draggingTaskId.value = null
  draggingTaskSource.value = null
  dragOverStateKey.value = null
  dragOverTaskId.value = null
  activeTaskTreeDropTarget.value = null
}

function handleColumnDragEnter(stateKey: WorkflowKey) {
  if (!draggingTaskId.value || draggingTaskSource.value?.source !== 'board') {
    return
  }

  dragOverStateKey.value = stateKey
}

function handleColumnDragOver(event: DragEvent, stateKey: WorkflowKey) {
  if (!draggingTaskId.value || draggingTaskSource.value?.source !== 'board') {
    return
  }

  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }

  dragOverStateKey.value = stateKey
}

function handleColumnDragLeave(stateKey: WorkflowKey) {
  if (dragOverStateKey.value !== stateKey) {
    return
  }

  dragOverStateKey.value = null
}

function handleTaskCardDragEnter(taskId: string) {
  if (!draggingTaskId.value || draggingTaskSource.value?.source !== 'board' || draggingTaskId.value === taskId) {
    return
  }

  dragOverTaskId.value = taskId
}

function handleTaskCardDragOver(event: DragEvent, taskId: string) {
  if (!draggingTaskId.value || draggingTaskSource.value?.source !== 'board' || draggingTaskId.value === taskId) {
    return
  }

  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }

  dragOverTaskId.value = taskId
}

function handleTaskCardDragLeave(taskId: string) {
  if (dragOverTaskId.value !== taskId) {
    return
  }

  dragOverTaskId.value = null
}

function handleTaskDrop(event: DragEvent, targetStateKey: WorkflowKey) {
  if (draggingTaskSource.value?.source !== 'board') {
    return
  }

  const taskId = draggingTaskId.value ?? event.dataTransfer?.getData('text/plain') ?? null
  if (!taskId) {
    return
  }

  const sourceColumn = props.columns.find((column) => column.tasks.some((task) => task.id === taskId))
  dragOverStateKey.value = null
  dragOverTaskId.value = null
  draggingTaskId.value = null

  if (!sourceColumn || sourceColumn.stateKey === targetStateKey) {
    return
  }

  emit('move-task-to-state', taskId, targetStateKey)
}

function handleTaskCardDrop(event: DragEvent, targetStateKey: WorkflowKey, targetTaskId: string) {
  if (draggingTaskSource.value?.source !== 'board') {
    return
  }

  const taskId = draggingTaskId.value ?? event.dataTransfer?.getData('text/plain') ?? null
  if (!taskId || taskId === targetTaskId) {
    return
  }

  const sourceColumn = props.columns.find((column) => column.tasks.some((task) => task.id === taskId))
  dragOverStateKey.value = null
  dragOverTaskId.value = null
  draggingTaskId.value = null

  if (!sourceColumn) {
    return
  }

  if (sourceColumn.stateKey === targetStateKey) {
    emit('reorder-task-within-column', taskId, targetTaskId)
    return
  }

  emit('move-task-to-state', taskId, targetStateKey)
}

function handleTaskTreeDragStart(event: DragEvent, taskId: string) {
  draggingTaskId.value = taskId
  draggingTaskSource.value = { taskId, source: 'tree' }

  if (!event.dataTransfer) {
    return
  }

  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData('text/plain', taskId)
}

function handleTaskTreeDragEnd() {
  draggingTaskId.value = null
  draggingTaskSource.value = null
  activeTaskTreeDropTarget.value = null
}

function handleTaskTreeDragOver(payload: TaskTreeDropEventPayload) {
  if (!draggingTaskId.value || draggingTaskSource.value?.source !== 'tree' || draggingTaskId.value === payload.taskId) {
    return
  }

  activeTaskTreeDropTarget.value = {
    taskId: payload.taskId,
    placement: payload.placement,
  }
}

function handleTaskTreeDragLeave(taskId: string) {
  if (activeTaskTreeDropTarget.value?.taskId !== taskId) {
    return
  }

  activeTaskTreeDropTarget.value = null
}

function handleTaskTreeDrop(payload: TaskTreeDropEventPayload) {
  if (!draggingTaskId.value || draggingTaskSource.value?.source !== 'tree' || draggingTaskId.value === payload.taskId) {
    handleTaskTreeDragEnd()
    return
  }

  const taskId = draggingTaskId.value
  activeTaskTreeDropTarget.value = null
  draggingTaskId.value = null
  draggingTaskSource.value = null

  if (payload.placement === 'inside') {
    emit('move-task-within-tree', taskId, {
      targetParentTaskId: payload.taskId,
      targetSiblingTaskId: null,
      insertAfter: false,
    })
    return
  }

  emit('move-task-within-tree', taskId, {
    targetParentTaskId: payload.parentTaskId,
    targetSiblingTaskId: payload.taskId,
    insertAfter: payload.placement === 'after',
  })
}
</script>
